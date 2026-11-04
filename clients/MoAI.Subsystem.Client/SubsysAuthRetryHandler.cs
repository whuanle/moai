using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;

namespace MoAI.Subsystem.Client;

/// <summary>
/// 认证重试处理器（业务管道专用）：
/// 1. 收到 401 时作废 token 缓存、取新 token 并重试一次（服务端吊销 / token 过期边界兜底）；
/// 2. 最终仍非 2xx 时读取原始错误体并抛出 <see cref="SubsysException"/>（替代 Kiota 泛型 ApiError）。
/// token 端点管道请使用 <see cref="SubsysErrorHandler"/>，避免与 token 管理器重入。
/// </summary>
public class SubsysAuthRetryHandler : SubsysHandlerBase
{
    private readonly ISubsysTokenProvider _tokenProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsysAuthRetryHandler"/> class.
    /// </summary>
    /// <param name="tokenProvider">token 管理器，用于作废缓存并重取 token。</param>
    public SubsysAuthRetryHandler(ISubsysTokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    /// <inheritdoc/>
    [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "克隆请求所有权转移给基类管道发送")]
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await BufferRequestContentAsync(request, cancellationToken);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return await EnsureSuccessAsync(response, cancellationToken);
        }

        _tokenProvider.Invalidate();

        // 重试请求在处理器链内发出，适配器不会重新认证：主动取新 token 替换 Authorization
        var freshToken = await _tokenProvider.GetAuthorizationTokenAsync(request.RequestUri!, cancellationToken: cancellationToken);
        var retryRequest = CloneRequest(request);
        retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", freshToken);
        response.Dispose();
        response = await base.SendAsync(retryRequest, cancellationToken);
        return await EnsureSuccessAsync(response, cancellationToken);
    }
}
