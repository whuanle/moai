using MoAI.Settings.Models;

namespace MoAI.Settings.Services;

/// <summary>
/// 邮箱验证配置读取服务，供业务模块感知平台邮件能力（SMTP 连接配置统一在通知中心渠道维护，系统设置仅绑定）.
/// </summary>
public interface IEmailSettingsService
{
    /// <summary>
    /// 获取邮箱验证配置（未启用或未绑定可用的 SMTP 渠道时 Enabled 为 false）.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回 <see cref="EmailSmtpSettings"/>.</returns>
    Task<EmailSmtpSettings> GetAsync(CancellationToken cancellationToken);
}
