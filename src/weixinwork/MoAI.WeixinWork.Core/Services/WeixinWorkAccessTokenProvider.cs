using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Infra.Exceptions;

namespace MoAI.WeixinWork.Services;

/// <summary>
/// 企业微信 access_token 提供者，按企业微信应用缓存 corp_access_token（提前 5 分钟过期）；
/// 凭证（CorpId/Secret/AgentId/API 地址）每次过期后从库中重读，连接更新后可调用 <see cref="Invalidate"/> 立即失效.
/// </summary>
[InjectOnSingleton]
public sealed partial class WeixinWorkAccessTokenProvider
{
    /// <summary>
    /// 企业微信 API 官方地址.
    /// </summary>
    public const string DefaultEndpoint = "https://qyapi.weixin.qq.com";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WeixinWorkAccessTokenProvider> _logger;
    private readonly ConcurrentDictionary<Guid, CachedToken> _tokens = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="WeixinWorkAccessTokenProvider"/> class.
    /// </summary>
    /// <param name="scopeFactory">作用域工厂.</param>
    /// <param name="httpClientFactory">HTTP 客户端工厂.</param>
    /// <param name="logger">日志.</param>
    public WeixinWorkAccessTokenProvider(IServiceScopeFactory scopeFactory, IHttpClientFactory httpClientFactory, ILogger<WeixinWorkAccessTokenProvider> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// 获取企业微信应用 access_token 与连接凭证快照.
    /// </summary>
    /// <param name="weixinWorkAppId">企业微信应用记录 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回 access_token 与连接凭证.</returns>
    public async Task<WeixinWorkAccessTokenResult> GetAsync(Guid weixinWorkAppId, CancellationToken cancellationToken = default)
    {
        if (_tokens.TryGetValue(weixinWorkAppId, out var cached) && cached.ExpireAt > DateTimeOffset.UtcNow)
        {
            return new WeixinWorkAccessTokenResult(cached.Token, cached.Credential);
        }

        var credential = await GetCredentialAsync(weixinWorkAppId, cancellationToken);

        var httpClient = _httpClientFactory.CreateClient(WeixinWorkApiClient.HttpClientName);
        using var response = await httpClient.GetAsync(
            $"{credential.Endpoint}/cgi-bin/gettoken?corpid={Uri.EscapeDataString(credential.CorpId)}&corpsecret={Uri.EscapeDataString(credential.CorpSecret)}",
            cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken);
        if (result == null || result.ErrCode != 0 || string.IsNullOrEmpty(result.AccessToken))
        {
            _logger.LogError(
                "获取企业微信 access_token 失败，weixinWorkAppId={WeixinWorkAppId} code={Code} msg={Msg}.",
                weixinWorkAppId,
                result?.ErrCode,
                result?.ErrMsg);
            throw new BusinessException($"获取企业微信访问凭证失败：{result?.ErrMsg ?? "响应为空"}") { StatusCode = 400 };
        }

        // 提前 5 分钟过期，避免边界失效
        var expireAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(result.ExpiresIn - 300, 60));
        _tokens[weixinWorkAppId] = new CachedToken(result.AccessToken!, expireAt, credential);
        return new WeixinWorkAccessTokenResult(result.AccessToken!, credential);
    }

    /// <summary>
    /// 立即失效缓存（连接凭证更新或 token 失效时调用）.
    /// </summary>
    /// <param name="weixinWorkAppId">企业微信应用记录 id.</param>
    public void Invalidate(Guid weixinWorkAppId)
    {
        _tokens.TryRemove(weixinWorkAppId, out _);
    }

    /// <summary>
    /// 查询企业微信应用凭证（企业 ID + Secret + AgentId + API 地址）.
    /// </summary>
    private async Task<WeixinWorkAppCredential> GetCredentialAsync(Guid weixinWorkAppId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var databaseContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        var app = await databaseContext.WeixinWorkApps
            .Where(x => x.Id == weixinWorkAppId)
            .Select(x => new { x.CorpId, x.CorpSecret, x.AgentId, x.ApiEndpoint })
            .FirstOrDefaultAsync(cancellationToken);

        if (app == null)
        {
            throw new BusinessException("企业微信应用连接不存在.") { StatusCode = 404 };
        }

        var endpoint = NormalizeEndpoint(app.ApiEndpoint);
        return new WeixinWorkAppCredential(weixinWorkAppId, app.CorpId, app.CorpSecret, app.AgentId, endpoint);
    }

    private static string NormalizeEndpoint(string apiEndpoint)
    {
        return string.IsNullOrWhiteSpace(apiEndpoint) ? DefaultEndpoint : apiEndpoint.Trim().TrimEnd('/');
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("errcode")] int ErrCode,
        [property: JsonPropertyName("errmsg")] string? ErrMsg,
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private sealed record CachedToken(string Token, DateTimeOffset ExpireAt, WeixinWorkAppCredential Credential);
}
