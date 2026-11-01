using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Notification.Models;

namespace MoAI.Notification.Senders;

/// <summary>
/// 企业微信群机器人发送器：POST webhook，Markdown 消息（内容上限 4096 字节，超长截断）.
/// </summary>
public class WecomWebhookSender : INotificationChannelSender
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WecomWebhookSender> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WecomWebhookSender"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP 客户端工厂.</param>
    /// <param name="logger">日志.</param>
    public WecomWebhookSender(IHttpClientFactory httpClientFactory, ILogger<WecomWebhookSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public NotificationChannelType Type => NotificationChannelType.WecomWebhook;

    /// <inheritdoc/>
    public async Task SendAsync(NotificationChannelEntity channel, NotificationMessage message, CancellationToken cancellationToken)
    {
        var config = NotificationChannelConfigs.Deserialize<WecomWebhookConfig>(channel.Config);

        var payload = new Dictionary<string, object>
        {
            ["msgtype"] = "markdown",
            ["markdown"] = new Dictionary<string, object>
            {
                ["content"] = NotificationText.Truncate(NotificationText.Markdown(message), 3000)
            }
        };

        var (statusCode, body) = await SenderHttp.PostJsonAsync(_httpClientFactory, config.WebhookUrl, payload, null, cancellationToken);
        SenderHttp.EnsureSuccessStatus(statusCode, body, "企业微信机器人");

        using var document = JsonDocument.Parse(body);
        var errcode = document.RootElement.TryGetProperty("errcode", out var errcodeElement) && errcodeElement.ValueKind == JsonValueKind.Number
            ? errcodeElement.GetInt64()
            : -1;
        if (errcode != 0)
        {
            var errmsg = document.RootElement.TryGetProperty("errmsg", out var errmsgElement) ? errmsgElement.GetString() ?? string.Empty : string.Empty;
            throw new BusinessException($"企业微信机器人返回错误：{errmsg}") { StatusCode = 502 };
        }

        _logger.LogDebug("企业微信机器人通知已发送：{ChannelName}", channel.Name);
    }
}
