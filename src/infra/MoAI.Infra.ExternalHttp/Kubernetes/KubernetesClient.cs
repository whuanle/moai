using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MoAI.Infra.Kubernetes;

/// <summary>
/// Kubernetes API 客户端实现：按次挂载 TLS 策略的 <see cref="HttpClientHandler"/> 到注入的
/// <see cref="ExternalHttpMessageHandler"/>（delegating）之下，非 2xx 归一为带状态码的 <see cref="HttpRequestException"/>.
/// </summary>
public class KubernetesClient : IKubernetesClient
{
    /// <summary>
    /// 具名 HttpClient（InfraExternalHttpModule 注册）.
    /// </summary>
    public const string HttpClientName = "KubernetesRpc";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ExternalHttpMessageHandler _externalHttpMessageHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="KubernetesClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HttpClient 工厂.</param>
    /// <param name="externalHttpMessageHandler">统一外部请求日志与遥测 handler（transient，按次实例化）.</param>
    public KubernetesClient(IHttpClientFactory httpClientFactory, ExternalHttpMessageHandler externalHttpMessageHandler)
    {
        _httpClientFactory = httpClientFactory;
        _externalHttpMessageHandler = externalHttpMessageHandler;
    }

    /// <inheritdoc/>
    public async Task<string> GetAsync(Uri endpoint, string? bearerToken, bool skipTlsVerify, CancellationToken cancellationToken = default)
    {
        using var inner = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = skipTlsVerify
                ? HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                : null,
        };
        _externalHttpMessageHandler.InnerHandler = inner;
        using var client = new HttpClient(_externalHttpMessageHandler, disposeHandler: false);
        client.Timeout = Timeout.InfiniteTimeSpan;

        using var message = new HttpRequestMessage(HttpMethod.Get, endpoint);
        if (!string.IsNullOrEmpty(bearerToken))
        {
            message.Headers.TryAddWithoutValidation("Authorization", $"Bearer {bearerToken}");
        }

        using var response = await client.SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Kubernetes 端点返回 HTTP {(int)response.StatusCode}：{content}", null, response.StatusCode);
        }

        return content;
    }
}
