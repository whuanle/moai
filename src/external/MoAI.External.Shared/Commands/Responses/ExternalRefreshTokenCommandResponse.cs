namespace MoAI.External.Commands.Responses;

/// <summary>
/// 外部接入刷新 Token 响应.
/// </summary>
public class ExternalRefreshTokenCommandResponse
{
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
