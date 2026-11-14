namespace MoAI.App.Queries.Responses;

/// <summary>
/// 系统级颁发接口响应：访问用户 token + 换算出的站内用户身份.
/// </summary>
public class SystemIssueAppAccessTokenCommandResponse
{
    /// <summary>
    /// 访问用户 access token，请求 /api/teamres 接口时以 Authorization: Bearer 携带.
    /// </summary>
    public string AccessToken { get; init; } = default!;

    /// <summary>
    /// 刷新令牌，用于换取新的 access_token 与 refresh_token.
    /// </summary>
    public string RefreshToken { get; init; } = default!;

    /// <summary>
    /// access token 有效秒数.
    /// </summary>
    public int ExpiresIn { get; init; }

    /// <summary>
    /// 换算出的站内用户 id.
    /// 渠道 + 第三方身份 → user_oauth_connection → user.id.
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    /// 站内用户名.
    /// </summary>
    public string UserName { get; init; } = default!;

    /// <summary>
    /// 站内用户昵称.
    /// </summary>
    public string? NickName { get; init; }

    /// <summary>
    /// token 绑定的目标应用 id.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 本次是否因第三方身份未绑定而自动创建了 MoAI 用户.
    /// </summary>
    public bool UserCreated { get; init; }
}
