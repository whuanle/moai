using MoAI.Settings.Models;

namespace MoAI.Settings.Services;

/// <summary>
/// SMTP 邮件服务配置读取服务，供业务模块感知平台通知邮件能力.
/// </summary>
public interface IEmailSettingsService
{
    /// <summary>
    /// 获取 SMTP 邮件服务配置（未启用时 Enabled 为 false，连接信息为空字符串/默认端口）.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回 <see cref="EmailSmtpSettings"/>.</returns>
    Task<EmailSmtpSettings> GetAsync(CancellationToken cancellationToken);
}
