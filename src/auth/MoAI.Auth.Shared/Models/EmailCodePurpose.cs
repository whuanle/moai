using System.Text.Json.Serialization;

namespace MoAI.Auth.Models;

/// <summary>
/// 邮箱验证码用途.
/// </summary>
public enum EmailCodePurpose
{
    /// <summary>
    /// 注册验证.
    /// </summary>
    [JsonPropertyName("register")]
    Register = 0,

    /// <summary>
    /// 找回密码验证.
    /// </summary>
    [JsonPropertyName("resetPassword")]
    ResetPassword = 1,

    /// <summary>
    /// 登录验证（仅限持有一次性登录票据的内部端点使用，公共发送端点拒绝该用途）.
    /// </summary>
    [JsonPropertyName("login")]
    Login = 2,
}
