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
}
