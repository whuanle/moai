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
    /// 判断本次登录是否需要邮箱验证码：策略命中且 SMTP 邮件服务已启用（after_fail 按当前失败计数达到阈值判定，阈值 LOGIN_EMAIL_CODE_FAIL_THRESHOLD）.
    /// </summary>
    /// <param name="loginFailCount">当前用户名累计登录失败次数.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>是否需要验证码.</returns>
    Task<bool> ShouldRequireLoginCodeAsync(int loginFailCount, CancellationToken cancellationToken);

    /// <summary>
    /// 图形验证码策略（LOGIN_CAPTCHA_MODE）.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>策略枚举.</returns>
    Task<LoginCaptchaMode> GetCaptchaModeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 判断本次操作是否需要图形验证码（分级递进的第一道防线，本地生成本地校验，成本低于邮箱验证码）：
    /// login 用途为用户名或 IP 任一有登录失败记录；register/forgot 用途为该 IP 本目的已发过一次验证码（首免费，再发需过码）.
    /// </summary>
    /// <param name="purpose">目的场景，取 <see cref="LoginCaptchaPurposes"/> 常量.</param>
    /// <param name="userName">用户名或邮箱（login 用途判定用户名失败计数用，可为空）.</param>
    /// <param name="ip">客户端 IP.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>是否需要图形验证码.</returns>
    Task<bool> ShouldRequireCaptchaAsync(string purpose, string userName, string ip, CancellationToken cancellationToken);

    /// <summary>
    /// 邮箱验证码 after_fail 触发阈值（LOGIN_EMAIL_CODE_FAIL_THRESHOLD，1~10）.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>失败次数阈值.</returns>
    Task<int> GetEmailCodeFailThresholdAsync(CancellationToken cancellationToken);
}
