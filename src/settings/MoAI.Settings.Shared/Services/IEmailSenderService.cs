using MoAI.Settings.Models;

namespace MoAI.Settings.Services;

/// <summary>
/// 平台通知邮件发送服务，使用系统设置中的 SMTP 邮件服务发送邮件.
/// </summary>
public interface IEmailSenderService
{
    /// <summary>
    /// 发送一封通知邮件（SMTP 服务未启用或未配置服务器时抛出业务异常）.
    /// </summary>
    /// <param name="message">邮件内容.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回 <see cref="EmailSendResult"/>.</returns>
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
