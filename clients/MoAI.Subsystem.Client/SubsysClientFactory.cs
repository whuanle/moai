using System.Diagnostics.CodeAnalysis;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using MoAI.Subsystem.Client.ApiSdk;

namespace MoAI.Subsystem.Client;

/// <summary>
/// 静态工厂：以既有 Bearer 凭据（如 SSO 用户委托 token）创建独立客户端实例，
/// 不参与 DI 与自动 token 管理。需要机机凭据自动管理时请用 <c>AddMoaiSubsysClient</c>。
/// </summary>
public static class SubsysClientFactory
{
    /// <summary>创建使用固定 Bearer token 的客户端实例。</summary>
    /// <param name="baseUrl">MoAI 服务主机根地址（不含 /api）。</param>
    /// <param name="accessToken">Bearer 凭据（用户委托 token 或自管凭据）。</param>
    /// <param name="httpClient">可选自定义 HttpClient（便于测试注入桩 handler）；缺省新建。</param>
    /// <returns>客户端实例。</returns>
    [SuppressMessage("Microsoft.Naming", "CA1054:Uri parameters should not be strings", Justification = "与 MoaiSubsysClientOptions.BaseUrl 保持一致，调用方传主机根地址字符串")]
    [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "adapter/client 所有权转移给调用方")]
    public static SubsysClient CreateWithAccessToken(string baseUrl, string accessToken, HttpClient? httpClient = null)
    {
        var adapter = new HttpClientRequestAdapter(
            new BaseBearerTokenAuthenticationProvider(new StaticAccessTokenProvider(accessToken)),
            httpClient: httpClient);
        adapter.BaseUrl = baseUrl;
        return new SubsysClient(adapter);
    }

    /// <summary>固定 token 提供器。</summary>
    private sealed class StaticAccessTokenProvider : IAccessTokenProvider
    {
        private readonly string _accessToken;

        public StaticAccessTokenProvider(string accessToken)
        {
            _accessToken = accessToken;
        }

        public AllowedHostsValidator AllowedHostsValidator => new();

        public Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_accessToken);
        }
    }
}
