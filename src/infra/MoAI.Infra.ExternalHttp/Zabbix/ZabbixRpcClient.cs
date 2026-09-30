using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MoAI.Infra.Zabbix;

/// <summary>
/// Zabbix JSON-RPC 客户端实现：按调用方给出的端点地址 POST 报文，非 2xx 归一为带状态码的 <see cref="HttpRequestException"/>.
/// </summary>
public class ZabbixRpcClient : IZabbixClient
{
    /// <summary>
    /// 具名 HttpClient（InfraExternalHttpModule 注册，挂统一的外部请求日志与遥测 handler）.
    /// </summary>
    public const string HttpClientName = "ZabbixRpc";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZabbixRpcClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HttpClient 工厂.</param>
    public ZabbixRpcClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc/>
    public async Task<string> RpcAsync(Uri endpoint, string? authorization, ZabbixRpcRequest request, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint);
        if (!string.IsNullOrEmpty(authorization))
        {
            message.Headers.TryAddWithoutValidation("Authorization", authorization);
        }

        message.Content = new StringContent(JsonSerializer.Serialize(request, SerializerOptions), Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Zabbix 端点返回 HTTP {(int)response.StatusCode}：{content}", null, response.StatusCode);
        }

        return content;
    }
}
