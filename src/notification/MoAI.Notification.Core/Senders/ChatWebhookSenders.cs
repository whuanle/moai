using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Notification.Models;

namespace MoAI.Notification.Senders;

/// <summary>
/// Slack Incoming Webhook 发送器：POST {"text": ...}，HTTP 2xx 视为成功.
/// </summary>
public class SlackWebhookSender : INotificationChannelSender
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SlackWebhookSender> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SlackWebhookSender"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP 客户端工厂.</param>
    /// <param name="logger">日志.</param>
    public SlackWebhookSender(IHttpClientFactory httpClientFactory, ILogger<SlackWebhookSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public NotificationChannelType Type => NotificationChannelType.Slack;

    /// <inheritdoc/>
    public async Task SendAsync(NotificationChannelEntity channel, NotificationMessage message, CancellationToken cancellationToken)
    {
        var config = NotificationChannelConfigs.Deserialize<SlackWebhookConfig>(channel.Config);
        var payload = new Dictionary<string, object> { ["text"] = NotificationText.Plain(message) };
        var (statusCode, body) = await SenderHttp.PostJsonAsync(_httpClientFactory, config.WebhookUrl, payload, null, cancellationToken);
        SenderHttp.EnsureSuccessStatus(statusCode, body, "Slack");
        _logger.LogDebug("Slack 通知已发送：{ChannelName}", channel.Name);
    }
}

/// <summary>
/// Discord Webhook 发送器：POST {"content": ...}（内容上限 2000 字符，超长截断），HTTP 2xx（含 204）视为成功.
/// </summary>
public class DiscordWebhookSender : INotificationChannelSender
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DiscordWebhookSender> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscordWebhookSender"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP 客户端工厂.</param>
    /// <param name="logger">日志.</param>
    public DiscordWebhookSender(IHttpClientFactory httpClientFactory, ILogger<DiscordWebhookSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public NotificationChannelType Type => NotificationChannelType.Discord;

    /// <inheritdoc/>
    public async Task SendAsync(NotificationChannelEntity channel, NotificationMessage message, CancellationToken cancellationToken)
    {
        var config = NotificationChannelConfigs.Deserialize<DiscordWebhookConfig>(channel.Config);
        var payload = new Dictionary<string, object> { ["content"] = NotificationText.Truncate(NotificationText.Plain(message), 1900) };
        var (statusCode, body) = await SenderHttp.PostJsonAsync(_httpClientFactory, config.WebhookUrl, payload, null, cancellationToken);
        SenderHttp.EnsureSuccessStatus(statusCode, body, "Discord");
        _logger.LogDebug("Discord 通知已发送：{ChannelName}", channel.Name);
    }
}

/// <summary>
/// Microsoft Teams（Workflows/Connector webhook）发送器：POST {"text": ...}，HTTP 2xx 视为成功.
/// </summary>
public class MicrosoftTeamsWebhookSender : INotificationChannelSender
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MicrosoftTeamsWebhookSender> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MicrosoftTeamsWebhookSender"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP 客户端工厂.</param>
    /// <param name="logger">日志.</param>
    public MicrosoftTeamsWebhookSender(IHttpClientFactory httpClientFactory, ILogger<MicrosoftTeamsWebhookSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public NotificationChannelType Type => NotificationChannelType.MicrosoftTeams;

    /// <inheritdoc/>
    public async Task SendAsync(NotificationChannelEntity channel, NotificationMessage message, CancellationToken cancellationToken)
    {
        var config = NotificationChannelConfigs.Deserialize<MicrosoftTeamsWebhookConfig>(channel.Config);
        var payload = new Dictionary<string, object> { ["text"] = NotificationText.Plain(message) };
        var (statusCode, body) = await SenderHttp.PostJsonAsync(_httpClientFactory, config.WebhookUrl, payload, null, cancellationToken);
        SenderHttp.EnsureSuccessStatus(statusCode, body, "Microsoft Teams");
        _logger.LogDebug("Microsoft Teams 通知已发送：{ChannelName}", channel.Name);
    }
}
