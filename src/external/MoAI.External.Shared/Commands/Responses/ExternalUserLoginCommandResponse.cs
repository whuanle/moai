namespace MoAI.External.Commands.Responses;

/// <summary>
/// 外部用户登录响应.
/// </summary>
public class ExternalUserLoginCommandResponse
{
    /// <summary>
    /// 外部用户ID.
    /// </summary>
    public string ExternalUserId { get; set; } = default!;

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
    /// 过期时间（毫秒时间戳）.
    /// </summary>
    public long ExpiresIn { get; set; }
}
