using System.Net.Mail;
using System.Text.Json;
using MoAI.Database.Enums;

namespace MoAI.Notification.Models;

/// <summary>
/// 飞书自定义机器人配置.
/// </summary>
public class FeishuWebhookConfig
{
    /// <summary>
    /// 机器人 webhook 地址（https）.
    /// </summary>
    public string WebhookUrl { get; init; } = string.Empty;

    /// <summary>
    /// 签名密钥（机器人未开启签名校验时留空）.
    /// </summary>
    public string Secret { get; init; } = string.Empty;
}

/// <summary>
/// 钉钉自定义机器人配置.
/// </summary>
public class DingTalkWebhookConfig
{
    /// <summary>
    /// 机器人 webhook 地址（https）.
    /// </summary>
    public string WebhookUrl { get; init; } = string.Empty;

    /// <summary>
    /// 加签密钥（SEC 开头，安全设置为加签时必填）.
    /// </summary>
    public string Secret { get; init; } = string.Empty;
}

/// <summary>
/// 企业微信群机器人配置.
/// </summary>
public class WecomWebhookConfig
{
    /// <summary>
    /// 机器人 webhook 地址（https，含 key 参数）.
    /// </summary>
    public string WebhookUrl { get; init; } = string.Empty;
}

/// <summary>
/// Telegram Bot 配置.
/// </summary>
public class TelegramBotConfig
{
    /// <summary>
    /// Bot API 地址，默认官方地址，可指向镜像.
    /// </summary>
    public string BaseUrl { get; init; } = "https://api.telegram.org";

    /// <summary>
    /// Bot token.
    /// </summary>
    public string BotToken { get; init; } = string.Empty;

    /// <summary>
    /// 目标 chat id（数字 id 或 @channelname）.
    /// </summary>
    public string ChatId { get; init; } = string.Empty;

    /// <summary>
    /// 消息解析模式，空为纯文本，可选 MarkdownV2 / HTML.
    /// </summary>
    public string ParseMode { get; init; } = string.Empty;
}

/// <summary>
/// 通用 webhook 配置.
/// </summary>
public class GenericWebhookConfig
{
    /// <summary>
    /// 请求地址（http/https，POST）.
    /// </summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>
    /// 自定义请求头.
    /// </summary>
    public Dictionary<string, string> Headers { get; init; } = new();

    /// <summary>
    /// 请求体模板，占位符 {title} {content} {level} {source} {time}；空则发送默认 JSON 结构.
    /// </summary>
    public string BodyTemplate { get; init; } = string.Empty;
}

/// <summary>
/// SMTP 邮件渠道配置（SMTP 服务器本身复用系统设置）.
/// </summary>
public class SmtpChannelConfig
{
    /// <summary>
    /// 收件人邮箱列表.
    /// </summary>
    public List<string> Recipients { get; init; } = new();

    /// <summary>
    /// 邮件主题前缀，空为【MoAI】.
    /// </summary>
    public string SubjectPrefix { get; init; } = string.Empty;
}

/// <summary>
/// 站内推送渠道配置（复用站内信）.
/// </summary>
public class InSiteChannelConfig
{
    /// <summary>
    /// 指定收件用户 id 集合.
    /// </summary>
    public List<long> UserIds { get; init; } = new();

    /// <summary>
    /// 是否同时推送全部管理员（admin + root）.
    /// </summary>
    public bool ToAdmins { get; init; }
}

/// <summary>
/// 渠道配置 JSON 解析与校验：按渠道类型反序列化为结构化配置并检查必填项.
/// </summary>
public static class NotificationChannelConfigs
{
    /// <summary>
    /// 配置 JSON 序列化选项（camelCase，忽略大小写）.
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 校验渠道配置 JSON 是否与类型匹配，返回错误消息，null 表示通过.
    /// </summary>
    /// <param name="type">渠道类型.</param>
    /// <param name="configJson">配置 JSON.</param>
    /// <returns>错误消息，null 表示通过.</returns>
    public static string? Validate(NotificationChannelType type, string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return "渠道配置不能为空";
        }

        try
        {
            switch (type)
            {
                case NotificationChannelType.FeishuWebhook:
                    var feishu = Deserialize<FeishuWebhookConfig>(configJson);
                    return CheckUrl(feishu.WebhookUrl, "webhookUrl");
                case NotificationChannelType.DingtalkWebhook:
                    var dingtalk = Deserialize<DingTalkWebhookConfig>(configJson);
                    return CheckUrl(dingtalk.WebhookUrl, "webhookUrl");
                case NotificationChannelType.WecomWebhook:
                    var wecom = Deserialize<WecomWebhookConfig>(configJson);
                    return CheckUrl(wecom.WebhookUrl, "webhookUrl");
                case NotificationChannelType.TelegramBot:
                    var telegram = Deserialize<TelegramBotConfig>(configJson);
                    if (string.IsNullOrWhiteSpace(telegram.BotToken))
                    {
                        return "botToken 不能为空";
                    }

                    if (string.IsNullOrWhiteSpace(telegram.ChatId))
                    {
                        return "chatId 不能为空";
                    }

                    return CheckUrl(string.IsNullOrWhiteSpace(telegram.BaseUrl) ? "https://api.telegram.org" : telegram.BaseUrl, "baseUrl");
                case NotificationChannelType.Webhook:
                    var webhook = Deserialize<GenericWebhookConfig>(configJson);
                    var urlError = CheckUrl(webhook.Url, "url");
                    if (urlError != null)
                    {
                        return urlError;
                    }

                    if (webhook.BodyTemplate.Length > 2000)
                    {
                        return "bodyTemplate 长度不能超过 2000";
                    }

                    return null;
                case NotificationChannelType.Smtp:
                    var smtp = Deserialize<SmtpChannelConfig>(configJson);
                    if (smtp.Recipients.Count == 0)
                    {
                        return "recipients 不能为空";
                    }

                    foreach (var recipient in smtp.Recipients)
                    {
                        if (!MailAddress.TryCreate(recipient, out _))
                        {
                            return $"recipients 包含无效邮箱：{recipient}";
                        }
                    }

                    return null;
                case NotificationChannelType.InSite:
                    var inSite = Deserialize<InSiteChannelConfig>(configJson);
                    if (inSite.UserIds.Count == 0 && !inSite.ToAdmins)
                    {
                        return "userIds 与 toAdmins 不能同时为空";
                    }

                    return null;
                default:
                    return "未知的通知渠道类型";
            }
        }
        catch (JsonException ex)
        {
            return $"渠道配置不是有效的 JSON：{ex.Message}";
        }
    }

    /// <summary>
    /// 反序列化渠道配置（调用前应先 <see cref="Validate"/> 通过）.
    /// </summary>
    /// <param name="configJson">配置 JSON.</param>
    /// <typeparam name="T">配置类型.</typeparam>
    /// <returns>返回配置对象.</returns>
    public static T Deserialize<T>(string configJson)
        where T : class, new()
        => JsonSerializer.Deserialize<T>(configJson, JsonOptions) ?? new T();

    /// <summary>
    /// 序列化渠道配置为 JSON（camelCase）.
    /// </summary>
    /// <param name="config">配置对象.</param>
    /// <typeparam name="T">配置类型.</typeparam>
    /// <returns>返回配置 JSON.</returns>
    public static string Serialize<T>(T config)
        where T : class
        => JsonSerializer.Serialize(config, JsonOptions);

    private static string? CheckUrl(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return $"{field} 不能为空";
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return $"{field} 必须是有效的 http(s) 地址";
        }

        return null;
    }
}
