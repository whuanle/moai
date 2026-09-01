using Refit;

namespace MoAI.Infra.OAuth;

/// <summary>
/// 用于创建基于动态 BaseAddress 的 OAuth Refit 客户端.
/// <para>OAuth 提供方的发现端点、令牌端点、用户信息端点各不相同，无法通过固定的 BaseAddress
/// 使用 AddHttpClient 注册，因此在调用时根据实际地址动态创建客户端。</para>
/// </summary>
public interface IOAuthClientFactory
{
    /// <summary>
    /// 根据目标地址的 Authority 部分创建发现/授权端点客户端.
    /// </summary>
    /// <param name="baseAddress">目标地址的 Authority 部分.</param>
    /// <returns>OAuth 客户端.</returns>
    IOAuthClient Create(Uri baseAddress);

    /// <summary>
    /// 根据目标地址的 Authority 部分创建令牌、用户信息端点客户端.
    /// </summary>
    /// <param name="baseAddress">目标地址的 Authority 部分.</param>
    /// <returns>OAuth AccessToken/UserInfo 客户端.</returns>
    IOAuthClientAccessToken CreateAccessTokenClient(Uri baseAddress);
}

/// <summary>
/// <see cref="IOAuthClientFactory"/> 的默认实现.
/// </summary>
public class OAuthClientFactory : IOAuthClientFactory
{
    /// <summary>
    /// 动态 OAuth 客户端使用的命名 HttpClient 名称，用于复用连接池与日志拦截器.
    /// </summary>
    public const string HttpClientName = "MoAI.OAuth";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RefitSettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthClientFactory"/> class.
    /// </summary>
    /// <param name="httpClientFactory"></param>
    /// <param name="settings"></param>
    public OAuthClientFactory(IHttpClientFactory httpClientFactory, RefitSettings settings)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
    }

    /// <inheritdoc/>
    public IOAuthClient Create(Uri baseAddress)
    {
        var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        httpClient.BaseAddress = baseAddress;
        return RestService.For<IOAuthClient>(httpClient, _settings);
    }

    /// <inheritdoc/>
    public IOAuthClientAccessToken CreateAccessTokenClient(Uri baseAddress)
    {
        var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        httpClient.BaseAddress = baseAddress;
        return RestService.For<IOAuthClientAccessToken>(httpClient, _settings);
    }
}
