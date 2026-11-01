using MoAI.Auth.Models;

namespace MoAI.Auth.Services;

/// <summary>
/// 登录安全策略服务：读取系统设置中的强制两步验证与登录邮箱验证码策略.
/// </summary>
public interface ILoginSecurityService
{
    /// <summary>
    /// 是否强制全员开启两步验证（LOGIN_FORCED_2FA）.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>是否强制.</returns>
    Task<bool> IsForced2faAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 登录邮箱验证码策略（LOGIN_EMAIL_CODE_MODE）.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>策略枚举.</returns>
    Task<LoginEmailCodeMode> GetEmailCodeModeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 判断本次登录是否需要邮箱验证码：策略命中且 SMTP 邮件服务已启用（after_fail 按当前失败计数判定）.
    /// </summary>
    /// <param name="loginFailCount">当前用户名累计登录失败次数.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>是否需要验证码.</returns>
    Task<bool> ShouldRequireLoginCodeAsync(int loginFailCount, CancellationToken cancellationToken);
}
