using System;
using System.Collections.Generic;
using System.Text;
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
/// 通用 webhook 发送器：POST 自定义 URL，支持自定义请求头与请求体模板（占位符 {title} {content} {level} {source} {time}），
/// 无模板时发送默认 JSON 结构 {"title","content","level","source","time"}，HTTP 2xx 视为成功.
/// </summary>
public class GenericWebhookSender : INotificationChannelSender
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GenericWebhookSender> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenericWebhookSender"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP 客户端工厂.</param>
    /// <param name="logger">日志.</param>
    public GenericWebhookSender(IHttpClientFactory httpClientFactory, ILogger<GenericWebhookSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public NotificationChannelType Type => NotificationChannelType.Webhook;

    /// <inheritdoc/>
    public async Task SendAsync(NotificationChannelEntity channel, NotificationMessage message, CancellationToken cancellationToken)
    {
        var config = NotificationChannelConfigs.Deserialize<GenericWebhookConfig>(channel.Config);

        string body;
        string contentType = "application/json; charset=utf-8";
        if (config.BodyTemplate.Length > 0)
        {
            body = config.BodyTemplate
                .Replace("{title}", message.Title)
                .Replace("{content}", message.Content)
                .Replace("{level}", NotificationText.LevelName(message.Level))
                .Replace("{source}", message.Source)
                .Replace("{time}", NotificationText.Time());

            if (!config.Headers.ContainsKey("Content-Type"))
            {
                contentType = "text/plain; charset=utf-8";
            }
        }
        else
        {
            body = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["title"] = message.Title,
                ["content"] = message.Content,
                ["level"] = NotificationText.LevelName(message.Level),
                ["source"] = message.Source,
                ["time"] = NotificationText.Time()
            });
        }

        var (statusCode, responseBody) = await SenderHttp.PostRawAsync(_httpClientFactory, config.Url, body, contentType, config.Headers, cancellationToken);
        SenderHttp.EnsureSuccessStatus(statusCode, responseBody, "Webhook");

        _logger.LogDebug("Webhook 通知已发送：{ChannelName}", channel.Name);
    }
}
