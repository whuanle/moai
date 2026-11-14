using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using MoAI.AIChannel.Commands;
using MoAI.AIChannel.Models;
using MoAI.AIChannel.Services;
using MoAI.Infra;
using MoAI.Infra.Exceptions;

namespace MoAI.AIChannel.Handlers;

/// <summary>
/// <inheritdoc cref="SyncAIModelCatalogCommand"/>
/// </summary>
public class SyncAIModelCatalogCommandHandler : IRequestHandler<SyncAIModelCatalogCommand, SyncAIModelCatalogCommandResponse>
{
    private const string LatestCatalogPath = "/api/portal/catalog/models/latest";
    private const int AttemptTimeoutSeconds = 60;
    private const long MaxCatalogSizeBytes = 64 * 1024 * 1024;

    // 优先尝试环境代理、系统默认代理，最后直连，兼容不同部署网络环境。
    private static readonly HttpClient? EnvProxyClient = CreateEnvProxyClient();
    private static readonly HttpClient SystemDefaultClient = new() { Timeout = TimeSpan.FromSeconds(AttemptTimeoutSeconds) };
    private static readonly HttpClient DirectClient = new(new SocketsHttpHandler
    {
        UseProxy = false,
        ConnectTimeout = TimeSpan.FromSeconds(AttemptTimeoutSeconds),
    })
    {
        Timeout = TimeSpan.FromSeconds(AttemptTimeoutSeconds),
    };

    private readonly AIModelCatalogService _catalogService;
    private readonly ILogger<SyncAIModelCatalogCommandHandler> _logger;
    private readonly string _platformEndpoint;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncAIModelCatalogCommandHandler"/> class.
    /// </summary>
    /// <param name="catalogService">模型目录服务.</param>
    /// <param name="logger">日志.</param>
    /// <param name="systemOptions">系统配置.</param>
    public SyncAIModelCatalogCommandHandler(
        AIModelCatalogService catalogService,
        ILogger<SyncAIModelCatalogCommandHandler> logger,
        SystemOptions systemOptions)
    {
        _catalogService = catalogService;
        _logger = logger;
        _platformEndpoint = systemOptions.MoPlatform.Endpoint;
    }

    /// <inheritdoc/>
    public async Task<SyncAIModelCatalogCommandResponse> Handle(SyncAIModelCatalogCommand request, CancellationToken cancellationToken)
    {
        var endpoint = ParseEndpoint(_platformEndpoint);
        var latestUrl = new Uri(endpoint, LatestCatalogPath);
        string latestRaw;
        try
        {
            latestRaw = await GetStringWithFallbackAsync(latestUrl, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "获取 MoPlatform 最新模型目录信息失败：{Url}", latestUrl);
            if (ex is HttpRequestException { StatusCode: HttpStatusCode.NotFound })
            {
                throw new BusinessException("MoPlatform 尚未发布模型目录，请先在平台刷新导出 models.json.") { StatusCode = 404 };
            }

            throw new BusinessException("同步模型目录失败，无法连接 MoPlatform，请检查服务地址或网络代理配置.") { StatusCode = 502 };
        }

        var pointer = ParseLatestPointer(latestRaw, endpoint);
        (byte[] Bytes, string Sha256, long Size) download;
        try
        {
            download = await GetBytesWithFallbackAsync(endpoint, latestUrl, pointer, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "下载 MoPlatform 模型目录失败：{Url}", GetSafeUrl(pointer.Url));
            throw new BusinessException("同步模型目录失败，无法下载 MoPlatform S3 文件；请检查对象存储网络、代理配置或签名地址有效期.") { StatusCode = 502 };
        }

        var bytes = download.Bytes;
        var expectedSha256 = download.Sha256;
        var expectedSize = download.Size;
        if (bytes.LongLength != expectedSize)
        {
            _logger.LogError("MoPlatform 模型目录大小校验失败，预期 {ExpectedSize} 字节，实际 {ActualSize} 字节", expectedSize, bytes.LongLength);
            throw new BusinessException("MoPlatform 模型目录文件大小校验失败，已放弃保存.") { StatusCode = 502 };
        }

        var actualSha256 = Convert.ToHexString(SHA256.HashData(bytes));
        if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError("MoPlatform 模型目录 SHA-256 校验失败，预期 {ExpectedSha256}，实际 {ActualSha256}", expectedSha256, actualSha256);
            throw new BusinessException("MoPlatform 模型目录文件校验失败，已放弃保存.") { StatusCode = 502 };
        }

        string raw;
        Dictionary<string, Dictionary<string, AIChannelModelMeta>> catalog;
        try
        {
            raw = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
            catalog = AIModelCatalogService.ParseCatalog(raw);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or DecoderFallbackException)
        {
            _logger.LogError(ex, "MoPlatform 返回的模型目录不是有效的 schemaVersion=1 models.json：{Url}", GetSafeUrl(pointer.Url));
            throw new BusinessException("MoPlatform 返回的模型目录格式无效，已放弃保存.") { StatusCode = 400 };
        }

        // 先写临时文件再原子替换，避免写一半时渠道同步读到损坏文件。
        Directory.CreateDirectory(AppConst.ConfigsPath);
        var target = Path.Combine(AppConst.ConfigsPath, "models.json");
        var temp = target + ".tmp";
        await File.WriteAllBytesAsync(temp, bytes, cancellationToken);
        File.Move(temp, target, overwrite: true);
        _catalogService.InvalidateCache();

        var modelCount = SumModelCount(catalog);
        _logger.LogInformation("MoPlatform 模型目录已同步到 {Path}：{Providers} 个渠道，{Models} 个模型", target, catalog.Count, modelCount);

        return new SyncAIModelCatalogCommandResponse
        {
            ProviderCount = catalog.Count,
            ModelCount = modelCount,
        };
    }

    private static Uri ParseEndpoint(string endpoint)
    {
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
        {
            return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute);
        }

        throw new BusinessException("MoPlatform 服务地址配置无效，请设置 MoAI:MoPlatform:Endpoint.") { StatusCode = 500 };
    }

    private static (Uri Url, string Sha256, long Size) ParseLatestPointer(string raw, Uri endpoint)
    {
        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;
            var url = root.GetProperty("url").GetString();
            var sha256 = root.GetProperty("sha256").GetString();
            var sizeElement = root.GetProperty("size");
            var sizeText = sizeElement.ValueKind == JsonValueKind.String ? sizeElement.GetString() : sizeElement.ToString();

            if (string.IsNullOrWhiteSpace(url)
                || string.IsNullOrWhiteSpace(sha256)
                || sha256.Length != 64
                || !long.TryParse(sizeText, out var size)
                || size <= 0
                || size > MaxCatalogSizeBytes)
            {
                throw new JsonException("MoPlatform latest 指针缺少有效的 url、sha256 或 size.");
            }

            var downloadUrl = Uri.TryCreate(url, UriKind.Absolute, out var absoluteUrl)
                ? absoluteUrl
                : new Uri(endpoint, url);
            if (downloadUrl.Scheme != Uri.UriSchemeHttps && downloadUrl.Scheme != Uri.UriSchemeHttp)
            {
                throw new JsonException("MoPlatform 模型目录 URL 必须使用 HTTP 或 HTTPS.");
            }

            // 兼容旧版 /static 地址：它必须走当前配置的平台入口（尤其是 Docker 内部地址）。
            // 新版 MoPlatform latest 已返回 S3 预签名地址，不会匹配此分支，保留其签名 host/query 原样下载。
            if (downloadUrl.AbsolutePath.StartsWith("/static/", StringComparison.OrdinalIgnoreCase))
            {
                var builder = new UriBuilder(downloadUrl)
                {
                    Scheme = endpoint.Scheme,
                    Host = endpoint.Host,
                    Port = endpoint.Port,
                    UserName = string.Empty,
                    Password = string.Empty,
                };
                downloadUrl = builder.Uri;
            }

            return (downloadUrl, sha256, size);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or UriFormatException)
        {
            throw new BusinessException("MoPlatform 返回的最新模型目录信息无效.") { StatusCode = 502 };
        }
    }

    private static HttpClient? CreateEnvProxyClient()
    {
        var proxy = Environment.GetEnvironmentVariable("HTTPS_PROXY")
            ?? Environment.GetEnvironmentVariable("https_proxy")
            ?? Environment.GetEnvironmentVariable("HTTP_PROXY")
            ?? Environment.GetEnvironmentVariable("http_proxy")
            ?? Environment.GetEnvironmentVariable("ALL_PROXY")
            ?? Environment.GetEnvironmentVariable("all_proxy");
        if (string.IsNullOrWhiteSpace(proxy))
        {
            return null;
        }

        return new HttpClient(new SocketsHttpHandler
        {
            Proxy = new WebProxy(proxy),
            ConnectTimeout = TimeSpan.FromSeconds(AttemptTimeoutSeconds),
        })
        {
            Timeout = TimeSpan.FromSeconds(AttemptTimeoutSeconds),
        };
    }

    private async Task<string> GetStringWithFallbackAsync(Uri url, CancellationToken cancellationToken)
    {
        var clients = GetClients();
        Exception? lastError = null;
        foreach (var client in clients)
        {
            try
            {
                return await client.GetStringAsync(url, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException ex) when (ex.StatusCode.HasValue && !IsTransientStatusCode(ex.StatusCode.Value))
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                lastError = ex;
                _logger.LogWarning("MoPlatform 请求尝试失败（{Client}）：{Message}", DescribeClient(client), ex.GetBaseException().Message);
            }
        }

        throw lastError ?? new HttpRequestException("MoPlatform 请求失败.");
    }

    private async Task<(byte[] Bytes, string Sha256, long Size)> GetBytesWithFallbackAsync(
        Uri endpoint,
        Uri latestUrl,
        (Uri Url, string Sha256, long Size) initialPointer,
        CancellationToken cancellationToken)
    {
        var clients = GetClients();
        Exception? lastError = null;
        var pointer = initialPointer;
        for (var index = 0; index < clients.Count; index++)
        {
            var client = clients[index];
            try
            {
                var bytes = await DownloadBytesAsync(client, pointer.Url, cancellationToken);
                return (bytes, pointer.Sha256, pointer.Size);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException ex) when (ex.StatusCode.HasValue && !IsRetryableDownloadStatusCode(ex.StatusCode.Value))
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                lastError = ex;
                _logger.LogWarning("MoPlatform 下载尝试失败（{Client}，{Url}）：{Message}", DescribeClient(client), GetSafeUrl(pointer.Url), ex.GetBaseException().Message);

                // 下载地址是短时效 S3 签名 URL。一次超时可能已耗尽其有效期，
                // 下一种网络路径必须先重新读取 latest，不能复用旧签名地址。
                if (index < clients.Count - 1)
                {
                    var refreshedLatestRaw = await GetStringWithFallbackAsync(latestUrl, cancellationToken);
                    pointer = ParseLatestPointer(refreshedLatestRaw, endpoint);
                }
            }
        }

        throw lastError ?? new HttpRequestException("MoPlatform 下载失败.");
    }

    private static string GetSafeUrl(Uri url)
    {
        // latest 指针可能包含短时效签名参数；日志只记录路径，避免泄露签名。
        return new UriBuilder(url) { UserName = string.Empty, Password = string.Empty, Query = string.Empty, Fragment = string.Empty }
            .Uri
            .GetLeftPart(UriPartial.Path);
    }

    private static async Task<byte[]> DownloadBytesAsync(HttpClient client, Uri url, CancellationToken cancellationToken)
    {
        // ResponseHeadersRead 不会让 HttpClient.Timeout 覆盖后续正文读取，因此为整个下载建立独立超时。
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(AttemptTimeoutSeconds));
        var downloadToken = timeout.Token;
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, downloadToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaxCatalogSizeBytes)
        {
            throw new HttpRequestException($"MoPlatform 模型目录超过 {MaxCatalogSizeBytes} 字节大小上限.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(downloadToken);
        using var content = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, downloadToken)) > 0)
        {
            if (content.Length + read > MaxCatalogSizeBytes)
            {
                throw new HttpRequestException($"MoPlatform 模型目录超过 {MaxCatalogSizeBytes} 字节大小上限.");
            }

            await content.WriteAsync(buffer.AsMemory(0, read), downloadToken);
        }

        return content.ToArray();
    }

    private static List<HttpClient> GetClients()
    {
        var clients = new List<HttpClient>();
        if (EnvProxyClient != null)
        {
            clients.Add(EnvProxyClient);
        }

        clients.Add(SystemDefaultClient);
        clients.Add(DirectClient);
        return clients;
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
    {
        var status = (int)statusCode;
        return status == 408 || status == 429 || status >= 500;
    }

    private static bool IsRetryableDownloadStatusCode(HttpStatusCode statusCode)
    {
        return IsTransientStatusCode(statusCode)
            || statusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound;
    }

    private static int SumModelCount(Dictionary<string, Dictionary<string, AIChannelModelMeta>> catalog)
    {
        var count = 0;
        foreach (var models in catalog.Values)
        {
            count += models.Count;
        }

        return count;
    }

    private static string DescribeClient(HttpClient client)
    {
        if (ReferenceEquals(client, DirectClient))
        {
            return "直连";
        }

        return ReferenceEquals(client, SystemDefaultClient) ? "系统默认代理" : "环境变量代理";
    }
}
