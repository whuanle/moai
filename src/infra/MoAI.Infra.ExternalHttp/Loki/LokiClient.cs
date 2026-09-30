using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MoAI.Infra.Loki;

/// <summary>
/// Grafana Loki HTTP API 客户端实现：按调用方给出的端点地址 GET，非 2xx 归一为带状态码的 <see cref="HttpRequestException"/>.
/// </summary>
public class LokiClient : ILokiClient
{
    /// <summary>
    /// 具名 HttpClient（InfraExternalHttpModule 注册，挂统一的外部请求日志与遥测 handler）.
    /// </summary>
    public const string HttpClientName = "LokiRpc";

    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="LokiClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HttpClient 工厂.</param>
    public LokiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc/>
    public async Task<string> GetAsync(Uri endpoint, string? authorization, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var message = new HttpRequestMessage(HttpMethod.Get, endpoint);
        if (!string.IsNullOrEmpty(authorization))
        {
            message.Headers.TryAddWithoutValidation("Authorization", authorization);
        }

        using var response = await client.SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Loki 端点返回 HTTP {(int)response.StatusCode}：{content}", null, response.StatusCode);
        }

        return content;
    }
}
