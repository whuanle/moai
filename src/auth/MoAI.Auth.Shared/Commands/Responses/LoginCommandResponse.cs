namespace MoAI.Auth.Commands.Responses;

/// <summary>
/// 登录结果.
/// </summary>
public class LoginCommandResponse
{
    /// <summary>
    /// 用户ID.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 用户名.
    /// </summary>
    public string UserName { get; set; } = default!;

    /// <summary>
    /// 访问令牌.
    /// </summary>
    public string AccessToken { get; set; } = default!;

    /// <summary>
    /// 刷新令牌.
    /// </summary>
    public string RefreshToken { get; set; } = default!;

    /// <summary>
    /// 令牌类型.
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// 过期时间（秒）.
    /// </summary>
    public long ExpiresIn { get; set; }

    /// <summary>
    /// 二次验证方式：none=无需（已颁发令牌）/ totp=认证器动态码 / emailCode=邮箱验证码；非 none 时令牌字段为空.
    /// </summary>
    public string SecondFactor { get; set; } = "none";

    /// <summary>
    /// 一次性登录票据（二次验证时返回，5 分钟有效）：随 login/verify 端点提交.
    /// </summary>
    public string LoginTicket { get; set; } = string.Empty;

    /// <summary>
    /// 是否待绑定两步验证（系统强制 2FA 且用户未绑定）：前端应强制进入个人设置绑定页.
    /// </summary>
    public bool MfaPending { get; set; }
}