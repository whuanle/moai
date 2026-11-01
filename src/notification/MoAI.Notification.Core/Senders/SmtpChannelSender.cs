using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Notification.Models;
using MoAI.Settings.Models;
using MoAI.Settings.Services;

namespace MoAI.Notification.Senders;

/// <summary>
/// SMTP 邮件渠道发送器：复用系统设置中的 SMTP 邮件服务，按渠道配置的收件人投递.
/// </summary>
public class SmtpChannelSender : INotificationChannelSender
{
    private readonly IEmailSenderService _emailSenderService;
    private readonly IEmailSettingsService _emailSettingsService;
    private readonly ILogger<SmtpChannelSender> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtpChannelSender"/> class.
    /// </summary>
    /// <param name="emailSenderService">邮件发送服务.</param>
    /// <param name="emailSettingsService">SMTP 设置读取服务.</param>
    /// <param name="logger">日志.</param>
    public SmtpChannelSender(IEmailSenderService emailSenderService, IEmailSettingsService emailSettingsService, ILogger<SmtpChannelSender> logger)
    {
        _emailSenderService = emailSenderService;
        _emailSettingsService = emailSettingsService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public NotificationChannelType Type => NotificationChannelType.Smtp;

    /// <inheritdoc/>
    public async Task SendAsync(NotificationChannelEntity channel, NotificationMessage message, CancellationToken cancellationToken)
    {
        var config = NotificationChannelConfigs.Deserialize<SmtpChannelConfig>(channel.Config);

        var settings = await _emailSettingsService.GetAsync(cancellationToken);
        if (!settings.Enabled)
        {
            throw new BusinessException("SMTP 邮件服务未启用，请先在系统设置中配置") { StatusCode = 502 };
        }

        var subject = config.SubjectPrefix.Length == 0 ? $"【MoAI】{message.Title}" : $"{config.SubjectPrefix} {message.Title}";
        await _emailSenderService.SendAsync(new EmailMessage
        {
            To = config.Recipients,
            Subject = subject,
            Body = NotificationText.Plain(message),
            IsHtml = false
        }, cancellationToken);

        _logger.LogDebug("SMTP 邮件通知已发送：{ChannelName}", channel.Name);
    }
}
