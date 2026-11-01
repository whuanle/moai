namespace MoAI.Auth.Models;

/// <summary>
/// 登录邮箱验证码策略（设置项 LOGIN_EMAIL_CODE_MODE）.
/// </summary>
public enum LoginEmailCodeMode
{
    /// <summary>
    /// 关闭（默认）.
    /// </summary>
    Off = 0,

    /// <summary>
    /// 每次登录都需要邮箱验证码.
    /// </summary>
    Always = 1,

    /// <summary>
    /// 密码错误一次后的登录需要邮箱验证码.
    /// </summary>
    AfterFail = 2,
}
