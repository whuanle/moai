namespace MoAI.Auth.Models;

/// <summary>
/// 登录二次验证方式常量（<see cref="Commands.Responses.LoginCommandResponse"/> 的 SecondFactor 与登录票据中的 Factor）.
/// </summary>
public static class LoginChallengeFactors
{
    /// <summary>
    /// 无需二次验证.
    /// </summary>
    public const string None = "none";

    /// <summary>
    /// 认证器动态码（TOTP）.
    /// </summary>
    public const string Totp = "totp";

    /// <summary>
    /// 邮箱验证码.
    /// </summary>
    public const string EmailCode = "emailCode";
}
