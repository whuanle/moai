using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Serialization;

namespace MoAI.Database.Enums;

/// <summary>
/// 通知渠道类型：通知中心支持的外部触达渠道，新渠道扩枚举接入.
/// </summary>
public enum NotificationChannelType
{
    /// <summary>
    /// 飞书自定义机器人 webhook.
    /// </summary>
    [JsonPropertyName("feishuWebhook")]
    FeishuWebhook = 1,

    /// <summary>
    /// 钉钉自定义机器人 webhook（成员名 DingtalkWebhook 使其 camelCase 与 json 名一致，NSwag 生成契约才不会分叉）.
    /// </summary>
    [JsonPropertyName("dingtalkWebhook")]
    DingtalkWebhook = 2,

    /// <summary>
    /// 企业微信群机器人 webhook.
    /// </summary>
    [JsonPropertyName("wecomWebhook")]
    WecomWebhook = 3,

    /// <summary>
    /// Telegram Bot（sendMessage 接口）.
    /// </summary>
    [JsonPropertyName("telegramBot")]
    TelegramBot = 4,

    /// <summary>
    /// 通用 webhook（自定义 URL/请求头/请求体模板）.
    /// </summary>
    [JsonPropertyName("webhook")]
    Webhook = 5,

    /// <summary>
    /// SMTP 邮件渠道（自带连接配置；系统设置「邮箱验证」仅绑定渠道 id）.
    /// </summary>
    [JsonPropertyName("smtp")]
    Smtp = 6,

    /// <summary>
    /// 站内推送（复用站内信，按用户/管理员投递）.
    /// </summary>
    [JsonPropertyName("inSite")]
    InSite = 7,

    /// <summary>
    /// Slack Incoming Webhook.
    /// </summary>
    [JsonPropertyName("slack")]
    Slack = 8,

    /// <summary>
    /// Discord Webhook.
    /// </summary>
    [JsonPropertyName("discord")]
    Discord = 9,

    /// <summary>
    /// Microsoft Teams（Workflows/Connector webhook）.
    /// </summary>
    [JsonPropertyName("microsoftTeams")]
    MicrosoftTeams = 10,

    /// <summary>
    /// Gotify 自建推送.
    /// </summary>
    [JsonPropertyName("gotify")]
    Gotify = 11,

    /// <summary>
    /// ntfy 推送.
    /// </summary>
    [JsonPropertyName("ntfy")]
    Ntfy = 12,

    /// <summary>
    /// Bark（iOS 推送）.
    /// </summary>
    [JsonPropertyName("bark")]
    Bark = 13,

    /// <summary>
    /// Server酱（Turbo）.
    /// </summary>
    [JsonPropertyName("serverChan")]
    ServerChan = 14,

    /// <summary>
    /// PushPlus（微信推送）.
    /// </summary>
    [JsonPropertyName("pushPlus")]
    PushPlus = 15,

    /// <summary>
    /// 飞书应用机器人（按用户绑定反查 union_id/open_id 单聊推送，渠道应用与登录应用可不同）.
    /// </summary>
    [JsonPropertyName("feishuAppBot")]
    FeishuAppBot = 16,

    /// <summary>
    /// 钉钉应用机器人（工作通知，按用户绑定 unionId 换算 userid 投递；成员名 DingtalkAppBot 使其 camelCase 与 json 名一致）.
    /// </summary>
    [JsonPropertyName("dingtalkAppBot")]
    DingtalkAppBot = 17,

    /// <summary>
    /// 企业微信应用机器人（应用消息，按用户绑定 userid 直投）.
    /// </summary>
    [JsonPropertyName("wecomAppBot")]
    WecomAppBot = 18,

    /// <summary>
    /// 阿里云短信（Dysmsapi SendSms，平台侧已审核模板 Code + 参数发送，收件人取渠道配置）.
    /// </summary>
    [JsonPropertyName("aliyunSms")]
    AliyunSms = 19,

    /// <summary>
    /// 腾讯云短信（SendSms，平台侧已审核模板 Id + 参数发送，收件人取渠道配置）.
    /// </summary>
    [JsonPropertyName("tencentSms")]
    TencentSms = 20,
}

/// <summary>
/// 通知内容类型：模板正文的形态，SMTP 渠道按此发送 HTML 邮件，其它渠道降级纯文本.
/// </summary>
public enum NotificationContentType
{
    /// <summary>
    /// 纯文本.
    /// </summary>
    [JsonPropertyName("text")]
    Text = 0,

    /// <summary>
    /// HTML（邮件模板用；非邮件渠道发送时自动去标签降级）.
    /// </summary>
    [JsonPropertyName("html")]
    Html = 1,
}

/// <summary>
/// 通知级别.
/// </summary>
public enum NotificationLevel
{
    /// <summary>
    /// 信息.
    /// </summary>
    [JsonPropertyName("info")]
    Info = 0,

    /// <summary>
    /// 警告.
    /// </summary>
    [JsonPropertyName("warning")]
    Warning = 1,

    /// <summary>
    /// 错误.
    /// </summary>
    [JsonPropertyName("error")]
    Error = 2,
}

/// <summary>
/// 通知发送状态.
/// </summary>
public enum NotificationSendStatus
{
    /// <summary>
    /// 待发送（已入队后台任务）.
    /// </summary>
    [JsonPropertyName("pending")]
    Pending = 0,

    /// <summary>
    /// 发送成功.
    /// </summary>
    [JsonPropertyName("success")]
    Success = 1,

    /// <summary>
    /// 发送失败.
    /// </summary>
    [JsonPropertyName("failed")]
    Failed = 2,
}

/// <summary>
/// 通知渠道类型解析：字符串（枚举名或 JsonPropertyName，忽略大小写）转枚举.
/// </summary>
public static class NotificationChannelTypes
{
    private static readonly Dictionary<string, NotificationChannelType> NameMap = BuildMap();

    /// <summary>
    /// 尝试解析渠道类型字符串（枚举名或 json 名，忽略大小写）.
    /// </summary>
    /// <param name="value">枚举名或 JsonPropertyName 字符串.</param>
    /// <param name="channelType">解析结果.</param>
    /// <returns>是否解析成功.</returns>
    public static bool TryParse(string? value, out NotificationChannelType channelType)
    {
        channelType = default;
        return !string.IsNullOrWhiteSpace(value) && NameMap.TryGetValue(value, out channelType);
    }

    private static Dictionary<string, NotificationChannelType> BuildMap()
    {
        var map = new Dictionary<string, NotificationChannelType>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in typeof(NotificationChannelType).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var value = (NotificationChannelType)field.GetValue(null)!;
            map[field.Name] = value;
            var jsonName = field.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
            if (!string.IsNullOrEmpty(jsonName))
            {
                map[jsonName] = value;
            }
        }

        return map;
    }
}
