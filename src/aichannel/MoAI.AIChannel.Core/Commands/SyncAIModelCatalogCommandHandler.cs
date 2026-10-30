using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
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
    /// <summary>
    /// opencode 官方模型目录源（models.json 最新版）.
    /// </summary>
    public const string CatalogSourceUrl = "https://models.opencode.ai/api.json";

    private const int AttemptTimeoutSeconds = 60;

    // 目录文件约 5MB，单次尝试 60s；环境变量代理 → 系统默认代理 → 直连逐级回退，
    // 兼容服务器走 HTTPS_PROXY、桌面机系统代理失效需直连、CN 网络必须走本地代理等场景
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

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncAIModelCatalogCommandHandler"/> class.
    /// </summary>
    /// <param name="catalogService">模型目录服务.</param>
    /// <param name="logger">日志.</param>
    public SyncAIModelCatalogCommandHandler(AIModelCatalogService catalogService, ILogger<SyncAIModelCatalogCommandHandler> logger)
    {
        _catalogService = catalogService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SyncAIModelCatalogCommandResponse> Handle(SyncAIModelCatalogCommand request, CancellationToken cancellationToken)
    {
        var raw = await DownloadRawAsync(cancellationToken);

        Dictionary<string, Dictionary<string, AIChannelModelMeta>> catalog;
        try
        {
            catalog = AIModelCatalogService.ParseCatalog(raw);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            _logger.LogError(ex, "下载的模型目录不是有效的 models.json 格式：{Url}", CatalogSourceUrl);
            throw new BusinessException("下载的模型目录不是有效的 models.json 格式，已放弃保存.") { StatusCode = 400 };
        }

        // 先写临时文件再原子替换，避免写一半时渠道同步读到损坏文件
        Directory.CreateDirectory(AppConst.ConfigsPath);
        var target = Path.Combine(AppConst.ConfigsPath, "models.json");
        var temp = target + ".tmp";
        await File.WriteAllTextAsync(temp, raw, cancellationToken);
        File.Move(temp, target, overwrite: true);

        // 失效缓存让新目录立即生效（服务本身也按文件时间戳自动重载，这里显式触发一次）
        _catalogService.InvalidateCache();

        _logger.LogInformation(
            "模型目录已同步到 {Path}：{Providers} 个渠道，{Models} 个模型",
            target,
            catalog.Count,
            catalog.Values.Sum(x => x.Count));

        return new SyncAIModelCatalogCommandResponse
        {
            ProviderCount = catalog.Count,
            ModelCount = catalog.Values.Sum(x => x.Count),
        };
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

    private async Task<string> DownloadRawAsync(CancellationToken cancellationToken)
    {
        var clients = new List<HttpClient>();
        if (EnvProxyClient != null)
        {
            clients.Add(EnvProxyClient);
        }

        clients.Add(SystemDefaultClient);
        clients.Add(DirectClient);

        Exception? lastError = null;
        foreach (var client in clients)
        {
            try
            {
                return await client.GetStringAsync(CatalogSourceUrl, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                lastError = ex;
                _logger.LogWarning("模型目录下载尝试失败（{Client}）：{Message}", DescribeClient(client), ex.GetBaseException().Message);
            }
        }

        _logger.LogError(lastError, "模型目录下载失败：{Url}", CatalogSourceUrl);
        throw new BusinessException("同步模型目录失败，无法连接模型目录源，请检查服务器网络或代理配置（HTTPS_PROXY）.") { StatusCode = 502 };
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
