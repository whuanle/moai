using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MoAI.Infra.ClickStack;

/// <summary>
/// ClickStack（HyperDX）对外 API 客户端实现：按调用方给出的端点地址 GET/POST，非 2xx 归一为带状态码的 <see cref="HttpRequestException"/>（消息内含响应正文，便于上层提取 error/message）.
/// </summary>
public class ClickStackApiClient : IClickStackClient
{
    /// <summary>
    /// 具名 HttpClient（InfraExternalHttpModule 注册，挂统一的外部请求日志与遥测 handler）.
    /// </summary>
    public const string HttpClientName = "ClickStackApi";

    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClickStackApiClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HttpClient 工厂.</param>
    public ClickStackApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc/>
    public async Task<string> GetAsync(Uri endpoint, string authorization, CancellationToken cancellationToken = default)
    {
        return await SendAsync(HttpMethod.Get, endpoint, authorization, body: null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<string> PostJsonAsync(Uri endpoint, string authorization, string jsonBody, CancellationToken cancellationToken = default)
    {
        return await SendAsync(HttpMethod.Post, endpoint, authorization, jsonBody, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> SendAsync(HttpMethod method, Uri endpoint, string authorization, string? body, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var message = new HttpRequestMessage(method, endpoint);
        if (!string.IsNullOrEmpty(authorization))
        {
            message.Headers.TryAddWithoutValidation("Authorization", authorization);
        }

        if (body != null)
        {
            message.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        using var response = await client.SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"ClickStack 端点返回 HTTP {(int)response.StatusCode}：{content}", null, response.StatusCode);
        }

        return content;
    }
}
