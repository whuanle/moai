namespace MoAI.Auth.Models;

/// <summary>
/// 图形验证码策略（设置项 LOGIN_CAPTCHA_MODE），覆盖登录提交与注册/找回密码发码.
/// </summary>
public enum LoginCaptchaMode
{
    /// <summary>
    /// 关闭（默认）.
    /// </summary>
    Off = 0,

    /// <summary>
    /// 始终需要图形验证码.
    /// </summary>
    Always = 1,

    /// <summary>
    /// 出现风险行为后需要：登录为用户名或 IP 有失败记录，注册/找回为该 IP 已发过验证码.
    /// </summary>
    AfterFail = 2,
}
