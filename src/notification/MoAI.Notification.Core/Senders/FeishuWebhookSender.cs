using System;
using System.Collections.Generic;
using System.Security.Cryptography;
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
/// 飞书自定义机器人发送器：POST webhook，文本消息，可选时间戳加签.
/// </summary>
public class FeishuWebhookSender : INotificationChannelSender
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FeishuWebhookSender> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeishuWebhookSender"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP 客户端工厂.</param>
    /// <param name="logger">日志.</param>
    public FeishuWebhookSender(IHttpClientFactory httpClientFactory, ILogger<FeishuWebhookSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public NotificationChannelType Type => NotificationChannelType.FeishuWebhook;

    /// <inheritdoc/>
    public async Task SendAsync(NotificationChannelEntity channel, NotificationMessage message, CancellationToken cancellationToken)
    {
        var config = NotificationChannelConfigs.Deserialize<FeishuWebhookConfig>(channel.Config);

        var payload = new Dictionary<string, object>
        {
            ["msg_type"] = "text",
            ["content"] = new Dictionary<string, object>
            {
                ["text"] = NotificationText.Plain(message)
            }
        };

        if (!string.IsNullOrEmpty(config.Secret))
        {
            var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            payload["timestamp"] = timestamp.ToString();
            payload["sign"] = FeishuSign(config.Secret, timestamp);
        }

        var (statusCode, body) = await SenderHttp.PostJsonAsync(_httpClientFactory, config.WebhookUrl, payload, null, cancellationToken);
        SenderHttp.EnsureSuccessStatus(statusCode, body, "飞书机器人");

        // 新版返回 {"code":0,"msg":"success"}，旧版返回 {"StatusCode":0,"StatusMessage":"success"}
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        long code = 0;
        string errorMessage = string.Empty;
        if (root.TryGetProperty("code", out var codeElement) && codeElement.ValueKind == JsonValueKind.Number)
        {
            code = codeElement.GetInt64();
            errorMessage = root.TryGetProperty("msg", out var msgElement) ? msgElement.GetString() ?? string.Empty : string.Empty;
        }
        else if (root.TryGetProperty("StatusCode", out var statusElement) && statusElement.ValueKind == JsonValueKind.Number)
        {
            code = statusElement.GetInt64();
            errorMessage = root.TryGetProperty("StatusMessage", out var statusMessageElement) ? statusMessageElement.GetString() ?? string.Empty : string.Empty;
        }

        if (code != 0)
        {
            throw new BusinessException($"飞书机器人返回错误：{errorMessage}") { StatusCode = 502 };
        }

        _logger.LogDebug("飞书机器人通知已发送：{ChannelName}", channel.Name);
    }

    /// <summary>
    /// 飞书自定义机器人加签：stringToSign = "{timestamp}\n{secret}"，以其为 HMAC-SHA256 密钥对空内容签名后 Base64.
    /// </summary>
    /// <param name="secret">签名密钥.</param>
    /// <param name="timestamp">Unix 秒时间戳.</param>
    /// <returns>返回签名.</returns>
    private static string FeishuSign(string secret, long timestamp)
    {
        var stringToSign = $"{timestamp}\n{secret}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(stringToSign));
        return Convert.ToBase64String(hmac.ComputeHash(Array.Empty<byte>()));
    }
}
