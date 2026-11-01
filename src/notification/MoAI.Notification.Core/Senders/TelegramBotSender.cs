using System;
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
/// Telegram Bot 发送器：调 sendMessage 接口，BaseUrl 可指向镜像.
/// </summary>
public class TelegramBotSender : INotificationChannelSender
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TelegramBotSender> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelegramBotSender"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP 客户端工厂.</param>
    /// <param name="logger">日志.</param>
    public TelegramBotSender(IHttpClientFactory httpClientFactory, ILogger<TelegramBotSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public NotificationChannelType Type => NotificationChannelType.TelegramBot;

    /// <inheritdoc/>
    public async Task SendAsync(NotificationChannelEntity channel, NotificationMessage message, CancellationToken cancellationToken)
    {
        var config = NotificationChannelConfigs.Deserialize<TelegramBotConfig>(channel.Config);

        var baseUrl = string.IsNullOrWhiteSpace(config.BaseUrl) ? "https://api.telegram.org" : config.BaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/bot{config.BotToken}/sendMessage";

        var payload = new Dictionary<string, object>
        {
            ["chat_id"] = config.ChatId,
            ["text"] = NotificationText.Plain(message)
        };

        if (!string.IsNullOrWhiteSpace(config.ParseMode))
        {
            payload["parse_mode"] = config.ParseMode;
        }

        var (statusCode, body) = await SenderHttp.PostJsonAsync(_httpClientFactory, url, payload, null, cancellationToken);
        SenderHttp.EnsureSuccessStatus(statusCode, body, "Telegram Bot");

        using var document = JsonDocument.Parse(body);
        var ok = document.RootElement.TryGetProperty("ok", out var okElement) && okElement.ValueKind == JsonValueKind.True;
        if (!ok)
        {
            var description = document.RootElement.TryGetProperty("description", out var descriptionElement) ? descriptionElement.GetString() ?? string.Empty : body;
            throw new BusinessException($"Telegram Bot 返回错误：{description}") { StatusCode = 502 };
        }

        _logger.LogDebug("Telegram Bot 通知已发送：{ChannelName}", channel.Name);
    }
}
