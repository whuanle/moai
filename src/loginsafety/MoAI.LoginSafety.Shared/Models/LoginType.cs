using System.Text.Json.Serialization;

namespace MoAI.LoginSafety.Models;

/// <summary>
/// 登录方式.
/// </summary>
public enum LoginType
{
    /// <summary>
    /// 账号密码登录.
    /// </summary>
    [JsonPropertyName("password")]
    Password = 0,

    /// <summary>
    /// 第三方 OAuth 登录（飞书/钉钉/企业微信）.
    /// </summary>
    [JsonPropertyName("oauth")]
    OAuth = 1,
}
