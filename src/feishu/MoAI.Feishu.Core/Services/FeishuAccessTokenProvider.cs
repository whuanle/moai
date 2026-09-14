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
using MoAI.Feishu.Services;
using MoAI.Infra.Exceptions;

namespace MoAI.Feishu.Services;

/// <summary>
/// 飞书租户访问凭证提供者实现，按飞书应用缓存 tenant_access_token.
/// </summary>
[InjectOnSingleton]
public sealed partial class FeishuAccessTokenProvider : IFeishuAccessTokenProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FeishuAccessTokenProvider> _logger;
    private readonly ConcurrentDictionary<Guid, CachedToken> _tokens = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="FeishuAccessTokenProvider"/> class.
    /// </summary>
    /// <param name="scopeFactory">作用域工厂.</param>
    /// <param name="httpClientFactory">HTTP 客户端工厂.</param>
    /// <param name="logger">日志.</param>
    public FeishuAccessTokenProvider(IServiceScopeFactory scopeFactory, IHttpClientFactory httpClientFactory, ILogger<FeishuAccessTokenProvider> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> GetTenantAccessTokenAsync(Guid feishuAppId, CancellationToken cancellationToken = default)
    {
        if (_tokens.TryGetValue(feishuAppId, out var cached) && cached.ExpireAt > DateTimeOffset.UtcNow)
        {
            return cached.Token;
        }

        var credential = await GetCredentialAsync(feishuAppId, cancellationToken);

        var httpClient = _httpClientFactory.CreateClient(FeishuApiClient.HttpClientName);
        using var content = JsonContent.Create(new TokenRequest(credential.AppId, credential.AppSecret));
        using var response = await httpClient.PostAsync(
            $"{credential.Domain}/open-apis/auth/v3/tenant_access_token/internal",
            content,
            cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken);
        if (result == null || result.Code != 0 || string.IsNullOrEmpty(result.TenantAccessToken))
        {
            _logger.LogError(
                "获取飞书 tenant_access_token 失败，feishuAppId={FeishuAppId} code={Code} msg={Msg}.",
                feishuAppId,
                result?.Code,
                result?.Msg);
            throw new BusinessException($"获取飞书访问凭证失败：{result?.Msg ?? "响应为空"}") { StatusCode = 400 };
        }

        // 提前 5 分钟过期，避免边界失效
        var expireAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(result.Expire - 300, 60));
        _tokens[feishuAppId] = new CachedToken(result.TenantAccessToken, expireAt);
        return result.TenantAccessToken;
    }

    /// <summary>
    /// 查询飞书应用凭证（域名 + AppID + AppSecret）.
    /// </summary>
    /// <param name="feishuAppId">飞书应用记录 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回应用凭证.</returns>
    public async Task<FeishuAppCredential> GetCredentialAsync(Guid feishuAppId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var databaseContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        var app = await databaseContext.FeishuApps
            .Where(x => x.Id == feishuAppId)
            .Select(x => new { x.AppId, x.AppSecret, x.Domain })
            .FirstOrDefaultAsync(cancellationToken);

        if (app == null)
        {
            throw new BusinessException("飞书应用不存在或已删除.") { StatusCode = 404 };
        }

        return new FeishuAppCredential(
            string.IsNullOrWhiteSpace(app.Domain) ? FeishuConnectionManager.DefaultDomain : app.Domain,
            app.AppId,
            app.AppSecret);
    }

    private sealed record CachedToken(string Token, DateTimeOffset ExpireAt);

    private sealed record TokenRequest([property: JsonPropertyName("app_id")] string AppId, [property: JsonPropertyName("app_secret")] string AppSecret);

    private sealed record TokenResponse(
        [property: JsonPropertyName("code")] int Code,
        [property: JsonPropertyName("msg")] string? Msg,
        [property: JsonPropertyName("tenant_access_token")] string? TenantAccessToken,
        [property: JsonPropertyName("expire")] int Expire);
}
