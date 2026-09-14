using MoAI.App.Models;

namespace MoAI.App.Queries.Responses;

/// <summary>
/// 外部 token 换取结果.
/// </summary>
public class ExternalTokenCommandResponse
{
    /// <summary>
    /// 访问令牌，请求 /api/external 接口时以 Authorization: Bearer 携带.
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
    /// token 类型：应用 token 或用户 token.
    /// </summary>
    public ExternalTokenType TokenType { get; init; }

    /// <summary>
    /// 外部用户 id（用户 token / 匿名 token），应用 token 为 null.
    /// </summary>
    public long? ExternalId { get; init; }

    /// <summary>
    /// 外部身份标识（用户 token / 匿名 token），应用 token 为 null.
    /// </summary>
    public string? ExternalUserId { get; init; }
}
