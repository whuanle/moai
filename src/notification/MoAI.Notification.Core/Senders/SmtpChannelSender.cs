using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Notification.Models;
using MoAI.Settings.Models;

namespace MoAI.Notification.Senders;

/// <summary>
/// SMTP 邮件渠道发送器：按渠道自包含的连接配置投递广播邮件（系统「邮箱验证」绑定亦复用同一配置模型）.
/// </summary>
public class SmtpChannelSender : INotificationChannelSender
{
    private readonly SmtpMailTransport _smtpMailTransport;
    private readonly ILogger<SmtpChannelSender> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtpChannelSender"/> class.
    /// </summary>
    /// <param name="smtpMailTransport">SMTP 邮件传输服务.</param>
    /// <param name="logger">日志.</param>
    public SmtpChannelSender(SmtpMailTransport smtpMailTransport, ILogger<SmtpChannelSender> logger)
    {
        _smtpMailTransport = smtpMailTransport;
        _logger = logger;
    }

    /// <inheritdoc/>
    public NotificationChannelType Type => NotificationChannelType.Smtp;

    /// <inheritdoc/>
    public async Task SendAsync(NotificationChannelEntity channel, NotificationMessage message, CancellationToken cancellationToken)
    {
        var config = NotificationChannelConfigs.Deserialize<SmtpChannelConfig>(channel.Config);

        var recipients = message.EmailRecipients?
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct()
            .ToList();
        if (recipients == null || recipients.Count == 0)
        {
            throw new InvalidOperationException("SMTP 渠道投递失败：未指定收件人邮箱（收件人由发送时动态提供）");
        }

        // 邮件主题即通知标题（模板标题或广播标题），不再叠加固定前缀
        var subject = message.Title;
        var isHtml = message.ContentType == NotificationContentType.Html;

        // HTML 模板正文自带完整样式与文案，不再叠加来源脚注；纯文本保持原渲染
        var body = isHtml ? message.Content : NotificationText.Plain(message);

        await _smtpMailTransport.SendAsync(config, new EmailMessage
        {
            To = recipients,
            Subject = subject,
            Body = body,
            IsHtml = isHtml
        }, cancellationToken);

        _logger.LogDebug("SMTP 邮件通知已发送：{ChannelName}，正文类型 {ContentType}", channel.Name, isHtml ? "html" : "text");
    }
}
