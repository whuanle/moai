using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using MoAI.Infra.Exceptions;
using MoAI.WeixinWork.Services;

namespace MoAI.WeixinWork.Services;

/// <summary>
/// 企业微信应用消息客户端实现：群聊走 appchat/send，单聊走 message/send（按连接配置的 API 地址）.
/// </summary>
[InjectOnSingleton]
public sealed class WeixinWorkApiClient : IWeixinWorkApiClient
{
    /// <summary>
    /// HTTP 客户端注册名.
    /// </summary>
    public const string HttpClientName = "MoAI.WeixinWork";

    /// <summary>
    /// 应用文本消息内容上限（UTF-8 字节），接口限制 2048 字节.
    /// </summary>
    public const int TextMaxBytes = 2048;

    /// <summary>
    /// token 失效错误码：40014 不合法的 access_token、42001 access_token 已过期.
    /// </summary>
    private static readonly int[] TokenExpiredErrCodes = { 40014, 42001 };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly WeixinWorkAccessTokenProvider _tokenProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeixinWorkApiClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP 客户端工厂.</param>
    /// <param name="tokenProvider">企业微信 access_token 提供者.</param>
    public WeixinWorkApiClient(IHttpClientFactory httpClientFactory, WeixinWorkAccessTokenProvider tokenProvider)
    {
        _httpClientFactory = httpClientFactory;
        _tokenProvider = tokenProvider;
    }

    /// <inheritdoc/>
    public async Task<bool> SendTextMessageAsync(Guid weixinWorkAppId, string toUserId, string chatId, string text, CancellationToken cancellationToken = default)
    {
        var content = TruncateUtf8(text ?? string.Empty, TextMaxBytes);

        var result = await SendAsync(weixinWorkAppId, toUserId, chatId, content, cancellationToken);
        if (result.ErrCode == 0)
        {
            return true;
        }

        // token 失效：立即失效缓存后重试一次
        if (TokenExpiredErrCodes.Contains(result.ErrCode))
        {
            _tokenProvider.Invalidate(weixinWorkAppId);
            result = await SendAsync(weixinWorkAppId, toUserId, chatId, content, cancellationToken);
            if (result.ErrCode == 0)
            {
                return true;
            }
        }

        throw new BusinessException($"企业微信消息发送失败：{result.ErrMsg ?? $"errcode={result.ErrCode}"}") { StatusCode = 400 };
    }

    private async Task<SendMessageResponse> SendAsync(Guid weixinWorkAppId, string toUserId, string chatId, string content, CancellationToken cancellationToken)
    {
        var tokenResult = await _tokenProvider.GetAsync(weixinWorkAppId, cancellationToken);
        var credential = tokenResult.Credential;

        var httpClient = _httpClientFactory.CreateClient(HttpClientName);

        // 群聊走应用推送群聊接口，单聊走应用消息接口（需 agentid）
        var path = string.IsNullOrEmpty(chatId) ? "/cgi-bin/message/send" : "/cgi-bin/appchat/send";
        var request = string.IsNullOrEmpty(chatId)
            ? SendMessageRequest.ForUser(toUserId, credential.AgentId, content)
            : SendMessageRequest.ForChat(chatId, content);

        using var response = await httpClient.PostAsJsonAsync(
            $"{credential.Endpoint}{path}?access_token={Uri.EscapeDataString(tokenResult.AccessToken)}",
            request,
            cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<SendMessageResponse>(cancellationToken: cancellationToken);
        if (result == null)
        {
            return new SendMessageResponse(-1, "响应为空");
        }

        return result;
    }

    /// <summary>
    /// 按 UTF-8 字节数截断文本，不截断代理对.
    /// </summary>
    private static string TruncateUtf8(string value, int maxBytes)
    {
        if (maxBytes <= 0)
        {
            return string.Empty;
        }

        var bytes = 0;
        var i = 0;
        while (i < value.Length)
        {
            var c = value[i];
            int size;
            if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                size = 4;
            }
            else
            {
                size = c <= 0x7F ? 1 : c <= 0x7FF ? 2 : 3;
            }

            if (bytes + size > maxBytes)
            {
                break;
            }

            bytes += size;
            i += size == 4 ? 2 : 1;
        }

        return value[..i];
    }

    private sealed class SendMessageRequest
    {
        private SendMessageRequest()
        {
        }

        public static SendMessageRequest ForUser(string toUserId, int agentId, string content) => new()
        {
            ToUser = toUserId,
            AgentId = agentId,
            MsgType = "text",
            Text = new TextContent(content),
        };

        public static SendMessageRequest ForChat(string chatId, string content) => new()
        {
            ChatId = chatId,
            MsgType = "text",
            Text = new TextContent(content),
        };

        [JsonPropertyName("touser")]
        public string? ToUser { get; private init; }

        [JsonPropertyName("chatid")]
        public string? ChatId { get; private init; }

        [JsonPropertyName("agentid")]
        public int? AgentId { get; private init; }

        [JsonPropertyName("msgtype")]
        public string MsgType { get; private init; } = "text";

        [JsonPropertyName("text")]
        public TextContent Text { get; private init; } = new(string.Empty);
    }

    private sealed record TextContent([property: JsonPropertyName("content")] string Content);

    private sealed record SendMessageResponse(
        [property: JsonPropertyName("errcode")] int ErrCode,
        [property: JsonPropertyName("errmsg")] string? ErrMsg);
}
