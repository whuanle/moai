using MoAI.Auth.Models;

namespace MoAI.Auth.Services;

/// <summary>
/// 邮箱验证码服务：生成并经 SMTP 发送验证码，Redis 存储校验，供注册与找回密码使用.
/// </summary>
public interface IEmailCodeService
{
    /// <summary>
    /// 注册是否需要邮箱验证码（SMTP 邮件服务已启用且注册验证码开关开启）.
    /// </summary>
    Task<bool> IsRegisterCodeRequiredAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 是否允许通过邮箱找回密码（仅取决于 SMTP 邮件服务是否启用）.
    /// </summary>
    Task<bool> IsPasswordResetEnabledAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 生成验证码并发送到邮箱（60 秒内同邮箱同用途不可重复发送）.
    /// </summary>
    /// <param name="purpose">验证码用途.</param>
    /// <param name="email">收件邮箱.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    Task SendCodeAsync(EmailCodePurpose purpose, string email, CancellationToken cancellationToken);

    /// <summary>
    /// 校验并消费验证码：验证通过后立即失效；错误次数超限或已过期抛出业务异常.
    /// </summary>
    /// <param name="purpose">验证码用途.</param>
    /// <param name="email">收件邮箱.</param>
    /// <param name="code">用户输入的验证码.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    Task VerifyAndConsumeAsync(EmailCodePurpose purpose, string email, string code, CancellationToken cancellationToken);
}
