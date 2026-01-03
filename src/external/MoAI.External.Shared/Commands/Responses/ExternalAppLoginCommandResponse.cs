namespace MoAI.External.Commands.Responses;

/// <summary>
/// 外部应用登录响应.
/// </summary>
public class ExternalAppLoginCommandResponse
{
    /// <summary>
    /// 应用ID.
    /// </summary>
    public Guid AppId { get; set; }

    /// <summary>
    /// 应用名称.
    /// </summary>
    public string AppName { get; set; } = default!;

    /// <summary>
    /// 所属团队ID.
    /// </summary>
    public int TeamId { get; set; }

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
