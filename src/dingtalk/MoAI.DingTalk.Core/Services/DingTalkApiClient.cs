using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using MoAI.Infra.Exceptions;

namespace MoAI.DingTalk.Services;

/// <summary>
/// 钉钉机器人消息客户端实现，通过会话 webhook 回复消息（单聊/群聊通用）.
/// </summary>
[InjectOnSingleton]
public sealed class DingTalkApiClient : IDingTalkApiClient
{
    /// <summary>
    /// HTTP 客户端注册名.
    /// </summary>
    public const string HttpClientName = "MoAI.DingTalk";

    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DingTalkApiClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP 客户端工厂.</param>
    public DingTalkApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc/>
    public async Task<bool> SendTextMessageAsync(string sessionWebhook, string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionWebhook))
        {
            throw new BusinessException("钉钉会话 webhook 为空，无法回复消息.") { StatusCode = 400 };
        }

        var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await httpClient.PostAsJsonAsync(
            sessionWebhook,
            new SendMessageRequest("text", new TextContent(text)),
            cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<SendMessageResponse>(cancellationToken: cancellationToken);
        if (result == null || result.ErrCode != 0)
        {
            throw new BusinessException($"钉钉消息发送失败：{result?.ErrMsg ?? "响应为空"}") { StatusCode = 400 };
        }

        return true;
    }

    private sealed record SendMessageRequest(
        [property: JsonPropertyName("msgtype")] string MsgType,
        [property: JsonPropertyName("text")] TextContent Text);

    private sealed record TextContent([property: JsonPropertyName("content")] string Content);

    private sealed record SendMessageResponse(
        [property: JsonPropertyName("errcode")] int ErrCode,
        [property: JsonPropertyName("errmsg")] string? ErrMsg);
}
