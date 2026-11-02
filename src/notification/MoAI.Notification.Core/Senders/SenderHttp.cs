using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MoAI.Infra.Exceptions;

namespace MoAI.Notification.Senders;

/// <summary>
/// 发送器共享 HTTP 工具：统一 HttpClient 名与 POST 行为.
/// </summary>
public static class SenderHttp
{
    /// <summary>
    /// HttpClient 名称（NotificationCoreModule 注册）.
    /// </summary>
    public const string HttpClientName = "moai-notification";

    /// <summary>
    /// GET 请求并读取响应文本.
    /// </summary>
    /// <param name="httpClientFactory">HTTP 客户端工厂.</param>
    /// <param name="url">请求地址（含查询串）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回状态码与响应体.</returns>
    public static async Task<(int StatusCode, string Body)> GetAsync(IHttpClientFactory httpClientFactory, string url, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.GetAsync(url, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        return ((int)response.StatusCode, responseBody);
    }

    /// <summary>
    /// POST JSON 并读取响应文本.
    /// </summary>
    /// <param name="httpClientFactory">HTTP 客户端工厂.</param>
    /// <param name="url">请求地址.</param>
    /// <param name="payload">JSON 载荷（将序列化）.</param>
    /// <param name="headers">附加请求头，可为空.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回状态码与响应体.</returns>
    public static async Task<(int StatusCode, string Body)> PostJsonAsync(IHttpClientFactory httpClientFactory, string url, object payload, IReadOnlyDictionary<string, string>? headers, CancellationToken cancellationToken)
        => await PostRawAsync(httpClientFactory, url, JsonSerializer.Serialize(payload), "application/json; charset=utf-8", headers, cancellationToken);

    /// <summary>
    /// POST 原始文本并读取响应文本.
    /// </summary>
    /// <param name="httpClientFactory">HTTP 客户端工厂.</param>
    /// <param name="url">请求地址.</param>
    /// <param name="body">请求体.</param>
    /// <param name="contentType">Content-Type.</param>
    /// <param name="headers">附加请求头（可覆盖 Content-Type），可为空.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回状态码与响应体.</returns>
    public static async Task<(int StatusCode, string Body)> PostRawAsync(IHttpClientFactory httpClientFactory, string url, string body, string contentType, IReadOnlyDictionary<string, string>? headers, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);

        var contentHeadersContentType = contentType;
        if (headers != null)
        {
            foreach (var header in headers)
            {
                if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    contentHeadersContentType = header.Value;
                }
            }
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body, Encoding.UTF8, MediaTypeHeaderValue.Parse(contentHeadersContentType).MediaType)
        };

        if (headers != null)
        {
            foreach (var header in headers)
            {
                if (!header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
        }

        using var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        return ((int)response.StatusCode, responseBody);
    }

    /// <summary>
    /// 校验 HTTP 状态码，非 2xx 抛出业务异常.
    /// </summary>
    /// <param name="statusCode">状态码.</param>
    /// <param name="body">响应体.</param>
    /// <param name="channelLabel">渠道展示名（错误消息用）.</param>
    public static void EnsureSuccessStatus(int statusCode, string body, string channelLabel)
    {
        if (statusCode is < 200 or >= 300)
        {
            throw new BusinessException($"{channelLabel} 请求失败，HTTP {statusCode}：{TruncateBody(body)}") { StatusCode = 502 };
        }
    }

    /// <summary>
    /// 截断响应体（错误消息防超长）.
    /// </summary>
    /// <param name="body">响应体.</param>
    /// <returns>返回截断后的文本.</returns>
    public static string TruncateBody(string body) => NotificationText.Truncate(body, 200);
}
