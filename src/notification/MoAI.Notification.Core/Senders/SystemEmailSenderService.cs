using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Notification.Models;
using MoAI.Settings.Models;
using MoAI.Settings.Services;

namespace MoAI.Notification.Senders;

/// <summary>
/// 平台系统邮件发送服务（<see cref="IEmailSenderService"/> 实现）：SMTP 连接配置统一在通知中心渠道维护，
/// 按系统设置「邮箱验证」绑定（SMTP_ENABLED + SMTP_CHANNEL_ID）定位渠道后经 <see cref="SmtpMailTransport"/> 发送.
/// </summary>
public class SystemEmailSenderService : IEmailSenderService
{
    private readonly IEmailSettingsService _emailSettingsService;
    private readonly DatabaseContext _databaseContext;
    private readonly SmtpMailTransport _smtpMailTransport;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemEmailSenderService"/> class.
    /// </summary>
    /// <param name="emailSettingsService">邮箱验证配置读取服务.</param>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="smtpMailTransport">SMTP 邮件传输服务.</param>
    public SystemEmailSenderService(IEmailSettingsService emailSettingsService, DatabaseContext databaseContext, SmtpMailTransport smtpMailTransport)
    {
        _emailSettingsService = emailSettingsService;
        _databaseContext = databaseContext;
        _smtpMailTransport = smtpMailTransport;
    }

    /// <inheritdoc/>
    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = await _emailSettingsService.GetAsync(cancellationToken);
        if (!settings.Enabled || settings.ChannelId <= 0)
        {
            throw new BusinessException("邮箱验证未启用或未绑定 SMTP 服务，请先在通知中心配置 SMTP 服务并在系统设置中绑定") { StatusCode = 400 };
        }

        var configJson = await _databaseContext.NotificationChannels
            .Where(c => c.Id == settings.ChannelId)
            .Select(c => c.Config)
            .FirstOrDefaultAsync(cancellationToken);
        if (configJson == null)
        {
            throw new BusinessException("绑定的 SMTP 服务不存在或已被删除，请重新在系统设置中绑定") { StatusCode = 400 };
        }

        var smtp = NotificationChannelConfigs.Deserialize<SmtpChannelConfig>(configJson);
        return await _smtpMailTransport.SendAsync(smtp, message, cancellationToken);
    }
}
