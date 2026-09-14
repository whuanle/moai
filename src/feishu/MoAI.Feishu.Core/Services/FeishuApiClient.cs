using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using MoAI.Feishu.Services;
using MoAI.Infra.Exceptions;

namespace MoAI.Feishu.Services;

/// <summary>
/// 飞书开放平台 API 客户端实现.
/// </summary>
[InjectOnSingleton]
public sealed partial class FeishuApiClient : IFeishuApiClient
{
    /// <summary>
    /// HTTP 客户端注册名.
    /// </summary>
    public const string HttpClientName = "MoAI.Feishu";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FeishuAccessTokenProvider _accessTokenProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeishuApiClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP 客户端工厂.</param>
    /// <param name="accessTokenProvider">租户访问凭证提供者.</param>
    public FeishuApiClient(IHttpClientFactory httpClientFactory, FeishuAccessTokenProvider accessTokenProvider)
    {
        _httpClientFactory = httpClientFactory;
        _accessTokenProvider = accessTokenProvider;
    }

    /// <inheritdoc/>
    public async Task<string> SendTextMessageAsync(Guid feishuAppId, string receiveIdType, string receiveId, string text, CancellationToken cancellationToken = default)
    {
        var credential = await _accessTokenProvider.GetCredentialAsync(feishuAppId, cancellationToken);
        var accessToken = await _accessTokenProvider.GetTenantAccessTokenAsync(feishuAppId, cancellationToken);

        var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{credential.Domain}/open-apis/im/v1/messages?receive_id_type={receiveIdType}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new SendMessageRequest(
            receiveId,
            "text",
            JsonSerializer.Serialize(new TextContent(text))));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<SendMessageResponse>(cancellationToken: cancellationToken);
        if (result == null || result.Code != 0 || result.Data == null || string.IsNullOrEmpty(result.Data.MessageId))
        {
            throw new BusinessException($"飞书消息发送失败：{result?.Msg ?? "响应为空"}") { StatusCode = 400 };
        }

        return result.Data.MessageId;
    }

    private sealed record SendMessageRequest(
        [property: JsonPropertyName("receive_id")] string ReceiveId,
        [property: JsonPropertyName("msg_type")] string MsgType,
        [property: JsonPropertyName("content")] string Content);

    private sealed record TextContent([property: JsonPropertyName("text")] string Text);

    private sealed record SendMessageResponse(
        [property: JsonPropertyName("code")] int Code,
        [property: JsonPropertyName("msg")] string? Msg,
        [property: JsonPropertyName("data")] SendMessageData? Data);

    private sealed record SendMessageData([property: JsonPropertyName("message_id")] string? MessageId);
}
