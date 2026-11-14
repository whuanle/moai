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
/// SMTP 邮件渠道配置（自包含 SMTP 连接配置，系统设置「邮箱验证」仅绑定渠道 id 使用）.
/// </summary>
public class SmtpChannelConfig
{
    /// <summary>
    /// SMTP 服务器地址（域名或 IP）.
    /// </summary>
    public string Host { get; init; } = string.Empty;

    /// <summary>
    /// SMTP 服务器端口，隐式 TLS 通常 465，STARTTLS 通常 587.
    /// </summary>
    public int Port { get; init; } = 465;

    /// <summary>
    /// 加密方式（ssl / starttls / none）.
    /// </summary>
    public string Security { get; init; } = "ssl";

    /// <summary>
    /// SMTP 登录账号（中继服务器免认证时可留空）.
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// SMTP 登录密码或授权码.
    /// </summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// 发件人邮箱地址（留空取登录账号）.
    /// </summary>
    public string FromAddress { get; init; } = string.Empty;

    /// <summary>
    /// 发件人显示名.
    /// </summary>
    public string FromName { get; init; } = string.Empty;
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
/// Slack Incoming Webhook 配置.
/// </summary>
public class SlackWebhookConfig
{
    /// <summary>
    /// Slack Incoming Webhook 地址（https）.
    /// </summary>
    public string WebhookUrl { get; init; } = string.Empty;
}

/// <summary>
/// Discord Webhook 配置.
/// </summary>
public class DiscordWebhookConfig
{
    /// <summary>
    /// Discord Webhook 地址（https，/webhooks/{id}/{token}）.
    /// </summary>
    public string WebhookUrl { get; init; } = string.Empty;
}

/// <summary>
/// Microsoft Teams（Workflows/Connector webhook）配置.
/// </summary>
public class MicrosoftTeamsWebhookConfig
{
    /// <summary>
    /// Teams Webhook 地址（https）.
    /// </summary>
    public string WebhookUrl { get; init; } = string.Empty;
}

/// <summary>
/// Gotify 自建推送配置.
/// </summary>
public class GotifyConfig
{
    /// <summary>
    /// Gotify 服务器地址（https）.
    /// </summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// 应用 Token.
    /// </summary>
    public string Token { get; init; } = string.Empty;
}

/// <summary>
/// ntfy 推送配置.
/// </summary>
public class NtfyConfig
{
    /// <summary>
    /// ntfy 服务器地址，默认官方 https://ntfy.sh，可自建.
    /// </summary>
    public string BaseUrl { get; init; } = "https://ntfy.sh";

    /// <summary>
    /// 订阅主题.
    /// </summary>
    public string Topic { get; init; } = string.Empty;
}

/// <summary>
/// Bark（iOS 推送）配置.
/// </summary>
public class BarkConfig
{
    /// <summary>
    /// bark-server 地址，默认官方 https://api.day.app，可自建.
    /// </summary>
    public string BaseUrl { get; init; } = "https://api.day.app";

    /// <summary>
    /// 设备 Key.
    /// </summary>
    public string DeviceKey { get; init; } = string.Empty;
}

/// <summary>
/// Server酱（Turbo）配置.
/// </summary>
public class ServerChanConfig
{
    /// <summary>
    /// Server酱 接口地址，默认 https://sctapi.ftqq.com.
    /// </summary>
    public string BaseUrl { get; init; } = "https://sctapi.ftqq.com";

    /// <summary>
    /// SendKey.
    /// </summary>
    public string SendKey { get; init; } = string.Empty;
}

/// <summary>
/// PushPlus 配置.
/// </summary>
public class PushPlusConfig
{
    /// <summary>
    /// PushPlus 接口地址，默认 https://www.pushplus.plus.
    /// </summary>
    public string BaseUrl { get; init; } = "https://www.pushplus.plus";

    /// <summary>
    /// 用户 Token.
    /// </summary>
    public string Token { get; init; } = string.Empty;
}

/// <summary>
/// 飞书应用机器人配置：凭证独立于第三方登录的连接，投递目标按用户绑定反查（union_id 优先，退回 open_id）.
/// </summary>
public class FeishuAppBotConfig
{
    /// <summary>
    /// 飞书开放平台地址，默认官方地址，可指向自建网关/桩.
    /// </summary>
    public string BaseUrl { get; init; } = "https://open.feishu.cn";

    /// <summary>
    /// 应用 App ID.
    /// </summary>
    public string AppId { get; init; } = string.Empty;

    /// <summary>
    /// 应用 App Secret.
    /// </summary>
    public string AppSecret { get; init; } = string.Empty;
}

/// <summary>
/// 钉钉应用机器人配置（工作通知）：unionId 换算企业内 userid 后按 agentId 投递.
/// </summary>
public class DingTalkAppBotConfig
{
    /// <summary>
    /// 钉钉服务地址，默认 https://oapi.dingtalk.com，可指向桩.
    /// </summary>
    public string BaseUrl { get; init; } = "https://oapi.dingtalk.com";

    /// <summary>
    /// 应用 AppKey.
    /// </summary>
    public string AppKey { get; init; } = string.Empty;

    /// <summary>
    /// 应用 App Secret.
    /// </summary>
    public string AppSecret { get; init; } = string.Empty;

    /// <summary>
    /// 应用 AgentId（工作通知接收方）.
    /// </summary>
    public string AgentId { get; init; } = string.Empty;
}

/// <summary>
/// 企业微信应用机器人配置（应用消息）：绑定 userid 直投指定 agentid.
/// </summary>
public class WecomAppBotConfig
{
    /// <summary>
    /// 企业微信服务地址，默认 https://qyapi.weixin.qq.com，可指向桩.
    /// </summary>
    public string BaseUrl { get; init; } = "https://qyapi.weixin.qq.com";

    /// <summary>
    /// 企业 CorpId.
    /// </summary>
    public string CorpId { get; init; } = string.Empty;

    /// <summary>
    /// 应用 Secret.
    /// </summary>
    public string CorpSecret { get; init; } = string.Empty;

    /// <summary>
    /// 应用 AgentId.
    /// </summary>
    public string AgentId { get; init; } = string.Empty;
}

/// <summary>
/// 阿里云短信配置（Dysmsapi SendSms，平台侧已审核模板，收件人发送时动态指定）.
/// </summary>
public class AliyunSmsConfig
{
    /// <summary>
    /// AccessKey Id.
    /// </summary>
    public string AccessKeyId { get; init; } = string.Empty;

    /// <summary>
    /// AccessKey Secret.
    /// </summary>
    public string AccessKeySecret { get; init; } = string.Empty;

    /// <summary>
    /// 短信签名名称（平台审核通过）.
    /// </summary>
    public string SignName { get; init; } = string.Empty;

    /// <summary>
    /// Dysmsapi 服务地址，默认官方地址，可指向自建网关/桩.
    /// </summary>
    public string Endpoint { get; init; } = "https://dysmsapi.aliyuncs.com";
}

/// <summary>
/// 腾讯云短信配置（SendSms，平台侧已审核模板，收件人发送时动态指定）.
/// </summary>
public class TencentSmsConfig
{
    /// <summary>
    /// SecretId.
    /// </summary>
    public string SecretId { get; init; } = string.Empty;

    /// <summary>
    /// SecretKey.
    /// </summary>
    public string SecretKey { get; init; } = string.Empty;

    /// <summary>
    /// 短信应用 SdkAppId.
    /// </summary>
    public string SmsSdkAppId { get; init; } = string.Empty;

    /// <summary>
    /// 短信签名名称（平台审核通过）.
    /// </summary>
    public string SignName { get; init; } = string.Empty;

    /// <summary>
    /// 短信服务地址，默认官方地址，可指向自建网关/桩.
    /// </summary>
    public string Endpoint { get; init; } = "https://sms.tencentcloudapi.com";
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
                    var smtpHost = smtp.Host.Trim();
                    if (smtpHost.Length == 0)
                    {
                        return "host 不能为空";
                    }

                    if (smtpHost.Length > 255)
                    {
                        return "host 长度不能超过 255";
                    }

                    if (smtp.Port < 1 || smtp.Port > 65535)
                    {
                        return "port 必须是 1~65535 之间的整数";
                    }

                    var smtpSecurity = smtp.Security.Trim().ToLowerInvariant();
                    if (smtpSecurity != "ssl" && smtpSecurity != "starttls" && smtpSecurity != "none")
                    {
                        return "security 必须是 ssl、starttls 或 none";
                    }

                    if (smtp.Username.Trim().Length > 320)
                    {
                        return "username 长度不能超过 320";
                    }

                    if (smtp.Password.Length > 200)
                    {
                        return "password 长度不能超过 200";
                    }

                    var smtpFromAddress = smtp.FromAddress.Trim();
                    if (smtpFromAddress.Length > 0 && (!MailAddress.TryCreate(smtpFromAddress, out _) || smtpFromAddress.Length > 320))
                    {
                        return "fromAddress 不是合法的邮箱地址";
                    }

                    if (smtp.FromName.Trim().Length > 100)
                    {
                        return "fromName 长度不能超过 100";
                    }

                    return null;
                case NotificationChannelType.InSite:
                    var inSite = Deserialize<InSiteChannelConfig>(configJson);
                    if (inSite.UserIds.Count == 0 && !inSite.ToAdmins)
                    {
                        return "userIds 与 toAdmins 不能同时为空";
                    }

                    return null;
                case NotificationChannelType.Slack:
                    var slack = Deserialize<SlackWebhookConfig>(configJson);
                    return CheckUrl(slack.WebhookUrl, "webhookUrl");
                case NotificationChannelType.Discord:
                    var discord = Deserialize<DiscordWebhookConfig>(configJson);
                    return CheckUrl(discord.WebhookUrl, "webhookUrl");
                case NotificationChannelType.MicrosoftTeams:
                    var teams = Deserialize<MicrosoftTeamsWebhookConfig>(configJson);
                    return CheckUrl(teams.WebhookUrl, "webhookUrl");
                case NotificationChannelType.Gotify:
                    var gotify = Deserialize<GotifyConfig>(configJson);
                    var gotifyUrlError = CheckUrl(gotify.BaseUrl, "baseUrl");
                    if (gotifyUrlError != null)
                    {
                        return gotifyUrlError;
                    }

                    return string.IsNullOrWhiteSpace(gotify.Token) ? "token 不能为空" : null;
                case NotificationChannelType.Ntfy:
                    var ntfy = Deserialize<NtfyConfig>(configJson);
                    var ntfyUrlError = CheckUrl(string.IsNullOrWhiteSpace(ntfy.BaseUrl) ? "https://ntfy.sh" : ntfy.BaseUrl, "baseUrl");
                    if (ntfyUrlError != null)
                    {
                        return ntfyUrlError;
                    }

                    return string.IsNullOrWhiteSpace(ntfy.Topic) ? "topic 不能为空" : null;
                case NotificationChannelType.Bark:
                    var bark = Deserialize<BarkConfig>(configJson);
                    var barkUrlError = CheckUrl(string.IsNullOrWhiteSpace(bark.BaseUrl) ? "https://api.day.app" : bark.BaseUrl, "baseUrl");
                    if (barkUrlError != null)
                    {
                        return barkUrlError;
                    }

                    return string.IsNullOrWhiteSpace(bark.DeviceKey) ? "deviceKey 不能为空" : null;
                case NotificationChannelType.ServerChan:
                    var serverChan = Deserialize<ServerChanConfig>(configJson);
                    var serverChanUrlError = CheckUrl(string.IsNullOrWhiteSpace(serverChan.BaseUrl) ? "https://sctapi.ftqq.com" : serverChan.BaseUrl, "baseUrl");
                    if (serverChanUrlError != null)
                    {
                        return serverChanUrlError;
                    }

                    return string.IsNullOrWhiteSpace(serverChan.SendKey) ? "sendKey 不能为空" : null;
                case NotificationChannelType.PushPlus:
                    var pushPlus = Deserialize<PushPlusConfig>(configJson);
                    var pushPlusUrlError = CheckUrl(string.IsNullOrWhiteSpace(pushPlus.BaseUrl) ? "https://www.pushplus.plus" : pushPlus.BaseUrl, "baseUrl");
                    if (pushPlusUrlError != null)
                    {
                        return pushPlusUrlError;
                    }

                    return string.IsNullOrWhiteSpace(pushPlus.Token) ? "token 不能为空" : null;
                case NotificationChannelType.FeishuAppBot:
                    var feishuApp = Deserialize<FeishuAppBotConfig>(configJson);
                    var feishuAppUrlError = CheckUrl(string.IsNullOrWhiteSpace(feishuApp.BaseUrl) ? "https://open.feishu.cn" : feishuApp.BaseUrl, "baseUrl");
                    if (feishuAppUrlError != null)
                    {
                        return feishuAppUrlError;
                    }

                    if (string.IsNullOrWhiteSpace(feishuApp.AppId))
                    {
                        return "appId 不能为空";
                    }

                    return string.IsNullOrWhiteSpace(feishuApp.AppSecret) ? "appSecret 不能为空" : null;
                case NotificationChannelType.DingtalkAppBot:
                    var dingTalkApp = Deserialize<DingTalkAppBotConfig>(configJson);
                    var dingTalkAppUrlError = CheckUrl(string.IsNullOrWhiteSpace(dingTalkApp.BaseUrl) ? "https://oapi.dingtalk.com" : dingTalkApp.BaseUrl, "baseUrl");
                    if (dingTalkAppUrlError != null)
                    {
                        return dingTalkAppUrlError;
                    }

                    if (string.IsNullOrWhiteSpace(dingTalkApp.AppKey))
                    {
                        return "appKey 不能为空";
                    }

                    if (string.IsNullOrWhiteSpace(dingTalkApp.AppSecret))
                    {
                        return "appSecret 不能为空";
                    }

                    return string.IsNullOrWhiteSpace(dingTalkApp.AgentId) ? "agentId 不能为空" : null;
                case NotificationChannelType.WecomAppBot:
                    var wecomApp = Deserialize<WecomAppBotConfig>(configJson);
                    var wecomAppUrlError = CheckUrl(string.IsNullOrWhiteSpace(wecomApp.BaseUrl) ? "https://qyapi.weixin.qq.com" : wecomApp.BaseUrl, "baseUrl");
                    if (wecomAppUrlError != null)
                    {
                        return wecomAppUrlError;
                    }

                    if (string.IsNullOrWhiteSpace(wecomApp.CorpId))
                    {
                        return "corpId 不能为空";
                    }

                    if (string.IsNullOrWhiteSpace(wecomApp.CorpSecret))
                    {
                        return "corpSecret 不能为空";
                    }

                    return string.IsNullOrWhiteSpace(wecomApp.AgentId) ? "agentId 不能为空" : null;
                case NotificationChannelType.AliyunSms:
                    var aliyunSms = Deserialize<AliyunSmsConfig>(configJson);
                    var aliyunSmsUrlError = CheckUrl(string.IsNullOrWhiteSpace(aliyunSms.Endpoint) ? "https://dysmsapi.aliyuncs.com" : aliyunSms.Endpoint, "endpoint");
                    if (aliyunSmsUrlError != null)
                    {
                        return aliyunSmsUrlError;
                    }

                    if (string.IsNullOrWhiteSpace(aliyunSms.AccessKeyId))
                    {
                        return "accessKeyId 不能为空";
                    }

                    if (string.IsNullOrWhiteSpace(aliyunSms.AccessKeySecret))
                    {
                        return "accessKeySecret 不能为空";
                    }

                    if (string.IsNullOrWhiteSpace(aliyunSms.SignName))
                    {
                        return "signName 不能为空";
                    }

                    return null;
                case NotificationChannelType.TencentSms:
                    var tencentSms = Deserialize<TencentSmsConfig>(configJson);
                    var tencentSmsUrlError = CheckUrl(string.IsNullOrWhiteSpace(tencentSms.Endpoint) ? "https://sms.tencentcloudapi.com" : tencentSms.Endpoint, "endpoint");
                    if (tencentSmsUrlError != null)
                    {
                        return tencentSmsUrlError;
                    }

                    if (string.IsNullOrWhiteSpace(tencentSms.SecretId))
                    {
                        return "secretId 不能为空";
                    }

                    if (string.IsNullOrWhiteSpace(tencentSms.SecretKey))
                    {
                        return "secretKey 不能为空";
                    }

                    if (string.IsNullOrWhiteSpace(tencentSms.SmsSdkAppId))
                    {
                        return "smsSdkAppId 不能为空";
                    }

                    if (string.IsNullOrWhiteSpace(tencentSms.SignName))
                    {
                        return "signName 不能为空";
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

    /// <summary>
    /// 校验动态收件人邮箱列表（SMTP 渠道发送时传入），返回错误消息，null 表示通过.
    /// </summary>
    /// <param name="recipients">收件人邮箱列表.</param>
    /// <returns>错误消息，null 表示通过.</returns>
    public static string? CheckEmailRecipients(IReadOnlyList<string>? recipients)
    {
        if (recipients == null || recipients.Count == 0)
        {
            return "收件人邮箱不能为空";
        }

        foreach (var recipient in recipients)
        {
            if (recipient.Trim().Length == 0 || recipient.Trim().Length > 320 || !MailAddress.TryCreate(recipient.Trim(), out _))
            {
                return $"收件人邮箱无效：{recipient}";
            }
        }

        return null;
    }

    /// <summary>
    /// 校验动态收件人手机号列表（短信渠道发送时传入），返回错误消息，null 表示通过.
    /// </summary>
    /// <param name="recipients">收件人手机号列表.</param>
    /// <returns>错误消息，null 表示通过.</returns>
    public static string? CheckPhoneRecipients(IReadOnlyList<string>? recipients)
    {
        if (recipients == null || recipients.Count == 0)
        {
            return "收件人手机号不能为空";
        }

        foreach (var recipient in recipients)
        {
            var phone = recipient.Trim();
            if (phone.Length is < 5 or > 20 || !phone.All(char.IsAsciiDigit))
            {
                return $"收件人手机号无效：{recipient}";
            }
        }

        return null;
    }
}
