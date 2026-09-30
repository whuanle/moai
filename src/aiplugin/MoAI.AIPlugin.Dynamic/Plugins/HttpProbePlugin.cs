using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.Exceptions;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// 可用性拨测（动态插件）：对 AI 指定目标做 HTTP 状态码/端口连通/域名解析三种拨测，让 AI 先回答「通不通」再决定下一步排障.
/// </summary>
/// <remarks>
/// 模式分派（<see cref="HttpProbeRequest.Mode"/>）：http / tcp / dns。
/// 拨测失败（连接拒绝、超时、解析失败、内网拒绝）不是异常而是 <c>Ok=false</c> + <c>Error</c> 的结论，
/// 仅参数非法抛业务异常；内网防护默认开启——目标解析到回环/RFC1918/链路本地/CGNAT/IPv6 ULA 即拒绝，
/// 需要拨测内网监控目标时在实例配置 AllowPrivateNetwork=true 显式放行。
/// 每次运行由 <c>PluginExecutor</c> 创建独立作用域实例化插件。
/// </remarks>
[AiPlugin(
    key: "http_probe",
    Name = "可用性拨测",
    Description = "对目标做可用性拨测：Mode 支持 http（URL 状态码/时延/正文预览）/tcp（端口连通）/dns（域名解析）；默认禁止拨测内网地址，实例配置 AllowPrivateNetwork=true 可放行；先用 {\"Mode\":\"http\",\"Url\":\"http://example.com/healthz\"} 起步")]
public class HttpProbePlugin : IDynamicPluginRuntime<HttpProbeRequest, HttpProbeResponse, HttpProbeConfig>
{
    /// <summary>拨测超时秒数的下界.</summary>
    private const int MinTimeoutSeconds = 1;

    /// <summary>拨测超时秒数的上界.</summary>
    private const int MaxTimeoutSeconds = 120;

    /// <summary>正文预览字符数的下界.</summary>
    private const int MinBodyPreviewChars = 0;

    /// <summary>正文预览字符数的上界.</summary>
    private const int MaxBodyPreviewChars = 4096;

    /// <summary>dns 模式地址列表上限.</summary>
    private const int MaxAddresses = 32;

    /// <summary>错误文本截断长度.</summary>
    private const int MaxErrorChars = 512;

    private static readonly HashSet<string> Modes = new(StringComparer.Ordinal)
    {
        "http", "tcp", "dns",
    };

    private static readonly HashSet<string> Methods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "HEAD",
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private HttpProbeConfig _config = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpProbePlugin"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HttpClient 工厂（http 模式按次创建客户端）.</param>
    public HttpProbePlugin(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Mode": "http",           // http | tcp | dns
              "Url": "http://host:8080/healthz", // http 模式：目标 URL（http/https）
              "Method": "GET",          // http 模式：GET / HEAD
              "Host": "db.internal",    // tcp/dns 模式：目标主机（域名或 IP）
              "Port": 6379              // tcp 模式：目标端口 1-65535
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "TimeoutSeconds": 10,    // 单次拨测超时秒数，1-120
              "BodyPreviewChars": 512, // http 模式正文预览最大字符数，0-4096（0=不返回）
              "AllowPrivateNetwork": false // 是否允许拨测内网地址（回环/私有网段/链路本地），默认拒绝
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(HttpProbeConfig config)
    {
        _config = new HttpProbeConfig
        {
            TimeoutSeconds = Math.Clamp(config.TimeoutSeconds, MinTimeoutSeconds, MaxTimeoutSeconds),
            BodyPreviewChars = Math.Clamp(config.BodyPreviewChars, MinBodyPreviewChars, MaxBodyPreviewChars),
            AllowPrivateNetwork = config.AllowPrivateNetwork,
        };

        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<HttpProbeResponse> RunAsync(HttpProbeRequest request, CancellationToken cancellationToken)
    {
        var mode = NormalizeMode(request.Mode);
        var stopwatch = Stopwatch.StartNew();
        var response = mode switch
        {
            "tcp" => await ProbeTcpAsync(request, cancellationToken).ConfigureAwait(false),
            "dns" => await ProbeDnsAsync(request, cancellationToken).ConfigureAwait(false),
            _ => await ProbeHttpAsync(request, cancellationToken).ConfigureAwait(false),
        };
        response.ElapsedMs = stopwatch.ElapsedMilliseconds;
        return response;
    }

    private static string NormalizeMode(string? mode)
    {
        var normalized = (mode ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0)
        {
            return "http";
        }

        if (!Modes.Contains(normalized))
        {
            throw new BusinessException(400, $"不支持的 Mode={normalized}：仅允许 http/tcp/dns");
        }

        return normalized;
    }

    /// <summary>
    /// 解析目标主机：字面 IP 直接返回，域名走系统 DNS.
    /// </summary>
    private static async Task<IPAddress[]> ResolveHostAsync(string host)
    {
        if (IPAddress.TryParse(host, out var literal))
        {
            return new[] { literal };
        }

        return await Dns.GetHostAddressesAsync(host).ConfigureAwait(false);
    }

    /// <summary>
    /// 内网防护检查：目标解析到内网地址时返回拒绝文案，放行返回 null.
    /// </summary>
    private static string? BuildPrivateNetworkError(IPAddress[] addresses)
    {
        foreach (var address in addresses)
        {
            if (HttpProbeGuard.IsPrivateAddress(address))
            {
                return $"目标解析到内网地址 {address}，已按内网防护拒绝拨测；如确需拨测内网监控目标，请在实例配置 AllowPrivateNetwork=true 放行";
            }
        }

        return null;
    }

    private static string TruncateError(string message)
    {
        return ObservabilityJson.Truncate(message, MaxErrorChars);
    }

    /// <summary>
    /// http 模式：请求目标 URL 并归一状态码/响应头/正文预览.
    /// </summary>
    private async Task<HttpProbeResponse> ProbeHttpAsync(HttpProbeRequest request, CancellationToken cancellationToken)
    {
        var url = (request.Url ?? string.Empty).Trim();
        if (url.Length == 0)
        {
            throw new BusinessException(400, "http 模式需要提供目标 URL Url");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new BusinessException(400, "Url 必须是合法的 http:// 或 https:// URL");
        }

        var method = string.IsNullOrWhiteSpace(request.Method) ? "GET" : request.Method.Trim().ToUpperInvariant();
        if (!Methods.Contains(method))
        {
            throw new BusinessException(400, $"http 模式仅支持 GET/HEAD，收到 {method}");
        }

        var response = new HttpProbeResponse { Mode = "http", Target = url };
        IPAddress[] addresses;
        try
        {
            addresses = await ResolveHostAsync(uri.Host).ConfigureAwait(false);
        }
        catch (SocketException ex)
        {
            response.Error = TruncateError($"域名解析失败：{ex.Message}");
            return response;
        }

        var privateNetworkError = _config.AllowPrivateNetwork ? null : BuildPrivateNetworkError(addresses);
        if (privateNetworkError != null)
        {
            response.Error = privateNetworkError;
            return response;
        }

        try
        {
            using var client = _httpClientFactory.CreateClient(nameof(HttpProbePlugin));
            client.Timeout = Timeout.InfiniteTimeSpan;
            using var timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(_config.TimeoutSeconds));
            using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
            using var httpRequest = new HttpRequestMessage(new HttpMethod(method), uri);
            using var httpResponse = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, linkedSource.Token).ConfigureAwait(false);

            response.StatusCode = (int)httpResponse.StatusCode;
            response.ReasonPhrase = httpResponse.ReasonPhrase;
            response.FinalUrl = httpResponse.RequestMessage?.RequestUri?.ToString();
            response.ContentType = httpResponse.Content?.Headers?.ContentType?.ToString();
            response.Server = httpResponse.Headers.Server.ToString();
            response.Ok = response.StatusCode < 400;
            await FillBodyPreviewAsync(response, httpResponse, method, linkedSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            response.Error = $"请求超时（{_config.TimeoutSeconds}s）";
        }
        catch (HttpRequestException ex)
        {
            response.Error = TruncateError($"连接失败：{ex.Message}");
        }

        return response;
    }

    /// <summary>
    /// 读取响应正文前 N 字节做预览（尽力而为，失败不影响拨测结论）.
    /// </summary>
    private async Task FillBodyPreviewAsync(HttpProbeResponse response, HttpResponseMessage httpResponse, string method, CancellationToken cancellationToken)
    {
        if (_config.BodyPreviewChars <= 0
            || string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase)
            || httpResponse.Content == null)
        {
            return;
        }

        try
        {
            await using var stream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var buffer = new byte[Math.Min(_config.BodyPreviewChars * 4, 16384)];
            var offset = 0;
            int read;
            while (offset < buffer.Length
                && (read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken).ConfigureAwait(false)) > 0)
            {
                offset += read;
            }

            response.BodyPreview = ObservabilityJson.Truncate(Encoding.UTF8.GetString(buffer, 0, offset), _config.BodyPreviewChars);
        }
        catch
        {
            // 正文预览是尽力而为的附加信息，读取失败不影响拨测结论.
        }
    }

    /// <summary>
    /// tcp 模式：尝试建立 TCP 连接并归一连通结论.
    /// </summary>
    private async Task<HttpProbeResponse> ProbeTcpAsync(HttpProbeRequest request, CancellationToken cancellationToken)
    {
        var host = (request.Host ?? string.Empty).Trim();
        if (host.Length == 0)
        {
            throw new BusinessException(400, "tcp 模式需要提供目标主机 Host");
        }

        if (request.Port < 1 || request.Port > 65535)
        {
            throw new BusinessException(400, $"tcp 模式端口必须在 1-65535，收到 {request.Port}");
        }

        var response = new HttpProbeResponse { Mode = "tcp", Target = $"{host}:{request.Port}" };
        IPAddress[] addresses;
        try
        {
            addresses = await ResolveHostAsync(host).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is SocketException)
        {
            response.Error = TruncateError($"域名解析失败：{ex.Message}");
            return response;
        }

        var privateNetworkError = _config.AllowPrivateNetwork ? null : BuildPrivateNetworkError(addresses);
        if (privateNetworkError != null)
        {
            response.Error = privateNetworkError;
            return response;
        }

        try
        {
            using var client = new TcpClient();
            using var timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(_config.TimeoutSeconds));
            using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
            await client.ConnectAsync(host, request.Port, linkedSource.Token).ConfigureAwait(false);
            response.Ok = true;
            if (client.Client.RemoteEndPoint is IPEndPoint endPoint)
            {
                var address = endPoint.Address.IsIPv4MappedToIPv6 ? endPoint.Address.MapToIPv4() : endPoint.Address;
                response.RemoteAddress = $"{address}:{endPoint.Port}";
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            response.Error = $"连接超时（{_config.TimeoutSeconds}s）";
        }
        catch (SocketException ex)
        {
            response.Error = TruncateError($"连接失败：{ex.Message}");
        }

        return response;
    }

    /// <summary>
    /// dns 模式：解析目标主机并列出地址.
    /// </summary>
    private async Task<HttpProbeResponse> ProbeDnsAsync(HttpProbeRequest request, CancellationToken cancellationToken)
    {
        var host = (request.Host ?? string.Empty).Trim();
        if (host.Length == 0)
        {
            throw new BusinessException(400, "dns 模式需要提供目标主机 Host");
        }

        var response = new HttpProbeResponse { Mode = "dns", Target = host };
        IPAddress[] addresses;
        try
        {
            addresses = await ResolveHostAsync(host).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is SocketException)
        {
            response.Error = TruncateError($"域名解析失败：{ex.Message}");
            return response;
        }

        var privateNetworkError = _config.AllowPrivateNetwork ? null : BuildPrivateNetworkError(addresses);
        if (privateNetworkError != null)
        {
            response.Error = privateNetworkError;
            return response;
        }

        response.Ok = addresses.Length > 0;
        var names = new List<string>();
        foreach (var address in addresses)
        {
            if (names.Count >= MaxAddresses)
            {
                break;
            }

            names.Add(address.ToString());
        }

        response.Addresses = names;
        return response;
    }
}
