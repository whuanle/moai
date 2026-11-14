namespace MoAI.Auth.Models;

/// <summary>
/// 图形验证码生成结果.
/// </summary>
public class CaptchaInfo
{
    /// <summary>
    /// 验证码 id，提交时随 <c>captchaCode</c> 一起传回.
    /// </summary>
    public string CaptchaId { get; init; } = default!;

    /// <summary>
    /// 验证码图片内容（base64 编码，不含 data: 前缀）.
    /// </summary>
    public string Image { get; init; } = default!;

    /// <summary>
    /// 图片 MIME 类型（image/svg+xml）.
    /// </summary>
    public string ImageMimeType { get; init; } = "image/svg+xml";

    /// <summary>
    /// 有效期（秒）.
    /// </summary>
    public int ExpiresInSeconds { get; init; }
}

/// <summary>
/// 图形验证码覆盖的目的场景常量（登录提交 / 注册发码 / 找回密码发码）.
/// </summary>
public static class LoginCaptchaPurposes
{
    /// <summary>
    /// 登录提交与登录邮箱验证码发送.
    /// </summary>
    public const string Login = "login";

    /// <summary>
    /// 注册页发码.
    /// </summary>
    public const string Register = "register";

    /// <summary>
    /// 找回密码页发码.
    /// </summary>
    public const string Forgot = "forgot";
}
