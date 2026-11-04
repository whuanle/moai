using System.Diagnostics.CodeAnalysis;
using System.Net;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using MoAI.Subsystem.Client.ApiSdk;
using MoAI.Subsystem.Client.ApiSdk.Models;

namespace MoAI.Subsystem.Client;

/// <summary>
/// 机器凭据 token 管理器默认实现：封装 <c>/api/subsys/token</c> 换取与 <c>/api/subsys/token/refresh</c> 轮换，
/// 并实现 Kiota 的 <see cref="IAccessTokenProvider"/>，由请求适配器在每次请求前取 Bearer。
/// 线程安全；到期前 <see cref="MoaiSubsysClientOptions.RefreshSkewSeconds"/> 窗口内主动刷新。
/// </summary>
public sealed class SubsysTokenProvider : ISubsysTokenProvider, IDisposable
{
    private readonly SubsysClient? _tokenClient;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly MoaiSubsysClientOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private TokenCache? _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsysTokenProvider"/> class.
    /// </summary>
    /// <param name="tokenClient">跑在匿名鉴权管道上的客户端实例（仅用于 token 端点）。</param>
    /// <param name="options">客户端配置。</param>
    /// <param name="timeProvider">时间源，测试可注入假时钟。</param>
    public SubsysTokenProvider(SubsysClient tokenClient, MoaiSubsysClientOptions options, TimeProvider? timeProvider = null)
    {
        _tokenClient = tokenClient;
        _options = options;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsysTokenProvider"/> class（DI 场景：token 客户端延迟到首次取 token 时构建，
    /// 避免在 HttpClientFactory 建 handler 链时嵌套创建其它命名客户端）。
    /// </summary>
    /// <param name="httpClientFactory">HttpClient 工厂。</param>
    /// <param name="options">客户端配置。</param>
    /// <param name="timeProvider">时间源，测试可注入假时钟。</param>
    public SubsysTokenProvider(IHttpClientFactory httpClientFactory, MoaiSubsysClientOptions options, TimeProvider? timeProvider = null)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public AllowedHostsValidator AllowedHostsValidator => new();

    /// <inheritdoc/>
    public void Invalidate()
    {
        // 仅作废 access token、保留 refreshToken：下一次取 token 优先走轮换，轮换失败自动回退全量换取
        var cache = _cache;
        if (cache is not null)
        {
            cache.ExpiresAt = DateTimeOffset.MinValue;
        }
    }

    /// <inheritdoc/>
    public async Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
    {
        // 应用接入 appid 直连：appid 本身即 Bearer 凭据，无需换取
        if (_options.IsAppIdDirect)
        {
            return _options.AppId;
        }

        var cache = _cache;
        if (cache is not null && IsUsable(cache))
        {
            return cache.AccessToken;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            cache = _cache;
            if (cache is not null && IsUsable(cache))
            {
                return cache.AccessToken;
            }

            _cache = await AcquireAsync(cache?.RefreshToken, cancellationToken);
            return _cache.AccessToken;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _gate.Dispose();
    }

    /// <summary>token 是否仍在「到期 - 提前刷新窗口」内可用。用加法比较避免 Invalidate 置 MinValue 后做减法溢出。</summary>
    private bool IsUsable(TokenCache cache)
    {
        return _timeProvider.GetUtcNow().AddSeconds(_options.RefreshSkewSeconds) < cache.ExpiresAt;
    }

    /// <summary>优先用 refreshToken 轮换；失败（吊销/过期/重置）回退 appid+secret 全量换取。</summary>
    private async Task<TokenCache> AcquireAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        var tokenClient = _tokenClient ?? CreateTokenClient();
        if (!string.IsNullOrEmpty(refreshToken))
        {
            try
            {
                var refreshed = await tokenClient.Api.Subsys.Token.Refresh.PostAsync(
                    new SubsystemTokenRefreshCommand { RefreshToken = refreshToken },
                    cancellationToken: cancellationToken);
                if (refreshed is not null)
                {
                    return ToCache(refreshed);
                }
            }
            catch (SubsysException)
            {
                // refreshToken 失效：走全量换取
            }
        }

        var response = await tokenClient.Api.Subsys.Token.PostAsync(
            new SubsystemTokenCommand { AppId = _options.AppId, Secret = _options.Secret },
            cancellationToken: cancellationToken) ?? throw new SubsysException(HttpStatusCode.Unauthorized, "token 换取返回空响应");
        return ToCache(response);
    }

    [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "adapter/client 所有权由缓存字段持有")]
    private SubsysClient CreateTokenClient()
    {
        var httpClient = _httpClientFactory!.CreateClient(SubsysClientServiceCollectionExtensions.TokenHttpClientName);
        var adapter = new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: httpClient);
        adapter.BaseUrl = _options.BaseUrl;
        return new SubsysClient(adapter);
    }

    private TokenCache ToCache(SubsystemTokenCommandResponse response)
    {
        var accessToken = response.AccessToken ?? throw new SubsysException(HttpStatusCode.Unauthorized, "token 换取响应缺少 accessToken");
        return new TokenCache
        {
            AccessToken = accessToken,
            RefreshToken = response.RefreshToken ?? string.Empty,
            ExpiresAt = _timeProvider.GetUtcNow() + TimeSpan.FromSeconds(Math.Max(1, response.ExpiresIn ?? 7200)),
        };
    }

    private sealed class TokenCache
    {
        public string AccessToken { get; set; } = string.Empty;

        public string RefreshToken { get; set; } = string.Empty;

        public DateTimeOffset ExpiresAt { get; set; }
    }
}

/// <summary>
/// 机器凭据 token 管理器：实现 Kiota 的 <see cref="IAccessTokenProvider"/>。
/// </summary>
public interface ISubsysTokenProvider : IAccessTokenProvider
{
    /// <summary>作废缓存的 access token。凭据被服务端吊销/重置后调用，下一次取 token 将重新刷新或换取。</summary>
    void Invalidate();
}
