using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
/// Gotify 自建推送发送器：POST {"baseUrl"}/message?token=...，载荷 {"title","message","priority"}（级别映射 3/6/9），HTTP 2xx 视为成功.
/// </summary>
public class GotifySender : INotificationChannelSender
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GotifySender> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GotifySender"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP 客户端工厂.</param>
    /// <param name="logger">日志.</param>
    public GotifySender(IHttpClientFactory httpClientFactory, ILogger<GotifySender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public NotificationChannelType Type => NotificationChannelType.Gotify;

    /// <inheritdoc/>
    public async Task SendAsync(NotificationChannelEntity channel, NotificationMessage message, CancellationToken cancellationToken)
    {
        var config = NotificationChannelConfigs.Deserialize<GotifyConfig>(channel.Config);
        var priority = message.Level switch
        {
            NotificationLevel.Warning => 6,
            NotificationLevel.Error => 9,
            _ => 3,
        };
        var payload = new Dictionary<string, object>
        {
            ["title"] = $"【MoAI】{NotificationText.Icon(message.Level)} {message.Title}",
            ["message"] = NotificationText.ContentText(message),
            ["priority"] = priority
        };
        var url = $"{config.BaseUrl.TrimEnd('/')}/message?token={config.Token}";
        var (statusCode, body) = await SenderHttp.PostJsonAsync(_httpClientFactory, url, payload, null, cancellationToken);
        SenderHttp.EnsureSuccessStatus(statusCode, body, "Gotify");
        _logger.LogDebug("Gotify 通知已发送：{ChannelName}", channel.Name);
    }
}

/// <summary>
/// ntfy 推送发送器：POST JSON 到根路径 {"topic","title","message","priority"}（级别映射 3/4/5），HTTP 2xx 视为成功.
/// </summary>
public class NtfySender : INotificationChannelSender
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NtfySender> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NtfySender"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP 客户端工厂.</param>
    /// <param name="logger">日志.</param>
    public NtfySender(IHttpClientFactory httpClientFactory, ILogger<NtfySender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public NotificationChannelType Type => NotificationChannelType.Ntfy;

    /// <inheritdoc/>
    public async Task SendAsync(NotificationChannelEntity channel, NotificationMessage message, CancellationToken cancellationToken)
    {
        var config = NotificationChannelConfigs.Deserialize<NtfyConfig>(channel.Config);
        var priority = message.Level switch
        {
            NotificationLevel.Warning => 4,
            NotificationLevel.Error => 5,
            _ => 3,
        };
        var baseUrl = string.IsNullOrWhiteSpace(config.BaseUrl) ? "https://ntfy.sh" : config.BaseUrl.TrimEnd('/');
        var payload = new Dictionary<string, object>
        {
            ["topic"] = config.Topic,
            ["title"] = $"【MoAI】{NotificationText.Icon(message.Level)} {message.Title}",
            ["message"] = NotificationText.ContentText(message),
            ["priority"] = priority
        };
        var (statusCode, body) = await SenderHttp.PostJsonAsync(_httpClientFactory, baseUrl, payload, null, cancellationToken);
        SenderHttp.EnsureSuccessStatus(statusCode, body, "ntfy");
        _logger.LogDebug("ntfy 通知已发送：{ChannelName}", channel.Name);
    }
}

/// <summary>
/// Bark（iOS 推送）发送器：POST {"baseUrl"}/push，载荷 {"device_key","title","body","group"}，要求响应 JSON code==200.
/// </summary>
public class BarkSender : INotificationChannelSender
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BarkSender> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BarkSender"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP 客户端工厂.</param>
    /// <param name="logger">日志.</param>
    public BarkSender(IHttpClientFactory httpClientFactory, ILogger<BarkSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public NotificationChannelType Type => NotificationChannelType.Bark;

    /// <inheritdoc/>
    public async Task SendAsync(NotificationChannelEntity channel, NotificationMessage message, CancellationToken cancellationToken)
    {
        var config = NotificationChannelConfigs.Deserialize<BarkConfig>(channel.Config);
        var baseUrl = string.IsNullOrWhiteSpace(config.BaseUrl) ? "https://api.day.app" : config.BaseUrl.TrimEnd('/');
        var payload = new Dictionary<string, object>
        {
            ["device_key"] = config.DeviceKey,
            ["title"] = $"【MoAI】{message.Title}",
            ["body"] = NotificationText.ContentText(message),
            ["group"] = "MoAI"
        };
        var url = $"{baseUrl}/push";
        var (statusCode, body) = await SenderHttp.PostJsonAsync(_httpClientFactory, url, payload, null, cancellationToken);
        SenderHttp.EnsureSuccessStatus(statusCode, body, "Bark");
        EnsureCode(body, "code", 200, "Bark");
        _logger.LogDebug("Bark 通知已发送：{ChannelName}", channel.Name);
    }

    /// <summary>
    /// 校验响应 JSON 中的整数字段值.
    /// </summary>
    internal static void EnsureCode(string body, string field, long expected, string channelLabel)
    {
        using var document = JsonDocument.Parse(body);
        var code = document.RootElement.TryGetProperty(field, out var codeElement) && codeElement.ValueKind == JsonValueKind.Number
            ? codeElement.GetInt64()
            : -1;
        if (code != expected)
        {
            throw new BusinessException($"{channelLabel} 返回错误：{SenderHttp.TruncateBody(body)}") { StatusCode = 502 };
        }
    }
}

/// <summary>
/// Server酱（Turbo）发送器：POST form 到 {"baseUrl"}/{"sendKey"}.send（title/desp），要求响应 JSON code==0.
/// </summary>
public class ServerChanSender : INotificationChannelSender
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ServerChanSender> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerChanSender"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP 客户端工厂.</param>
    /// <param name="logger">日志.</param>
    public ServerChanSender(IHttpClientFactory httpClientFactory, ILogger<ServerChanSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public NotificationChannelType Type => NotificationChannelType.ServerChan;

    /// <inheritdoc/>
    public async Task SendAsync(NotificationChannelEntity channel, NotificationMessage message, CancellationToken cancellationToken)
    {
        var config = NotificationChannelConfigs.Deserialize<ServerChanConfig>(channel.Config);
        var baseUrl = string.IsNullOrWhiteSpace(config.BaseUrl) ? "https://sctapi.ftqq.com" : config.BaseUrl.TrimEnd('/');
        var pairs = new Dictionary<string, string>
        {
            ["title"] = NotificationText.Truncate($"【MoAI】{message.Title}", 32),
            ["desp"] = NotificationText.Plain(message)
        };
        var form = string.Join("&", pairs.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        var url = $"{baseUrl}/{config.SendKey}.send";
        var (statusCode, body) = await SenderHttp.PostRawAsync(_httpClientFactory, url, form, "application/x-www-form-urlencoded; charset=utf-8", null, cancellationToken);
        SenderHttp.EnsureSuccessStatus(statusCode, body, "Server酱");
        BarkSender.EnsureCode(body, "code", 0, "Server酱");
        _logger.LogDebug("Server酱 通知已发送：{ChannelName}", channel.Name);
    }
}

/// <summary>
/// PushPlus（微信推送）发送器：POST JSON 到 {"baseUrl"}/send（token/title/content/template=txt），要求响应 JSON code==200.
/// </summary>
public class PushPlusSender : INotificationChannelSender
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PushPlusSender> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PushPlusSender"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP 客户端工厂.</param>
    /// <param name="logger">日志.</param>
    public PushPlusSender(IHttpClientFactory httpClientFactory, ILogger<PushPlusSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public NotificationChannelType Type => NotificationChannelType.PushPlus;

    /// <inheritdoc/>
    public async Task SendAsync(NotificationChannelEntity channel, NotificationMessage message, CancellationToken cancellationToken)
    {
        var config = NotificationChannelConfigs.Deserialize<PushPlusConfig>(channel.Config);
        var baseUrl = string.IsNullOrWhiteSpace(config.BaseUrl) ? "https://www.pushplus.plus" : config.BaseUrl.TrimEnd('/');
        var payload = new Dictionary<string, object>
        {
            ["token"] = config.Token,
            ["title"] = $"【MoAI】{message.Title}",
            ["content"] = NotificationText.Plain(message),
            ["template"] = "txt"
        };
        var url = $"{baseUrl}/send";
        var (statusCode, body) = await SenderHttp.PostJsonAsync(_httpClientFactory, url, payload, null, cancellationToken);
        SenderHttp.EnsureSuccessStatus(statusCode, body, "PushPlus");
        BarkSender.EnsureCode(body, "code", 200, "PushPlus");
        _logger.LogDebug("PushPlus 通知已发送：{ChannelName}", channel.Name);
    }
}
