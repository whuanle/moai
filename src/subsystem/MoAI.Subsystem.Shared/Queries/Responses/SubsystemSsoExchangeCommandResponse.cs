namespace MoAI.Subsystem.Queries.Responses;

/// <summary>
/// 委托码兑换/刷新结果：用户委托 token 与 MoAI 用户基本资料.
/// </summary>
public class SubsystemSsoExchangeCommandResponse
{
    /// <summary>
    /// 用户委托 access token（机器面/资源面调用由子系统后端代持）.
    /// </summary>
    public string AccessToken { get; init; } = default!;

    /// <summary>
    /// 用户委托 refresh token.
    /// </summary>
    public string RefreshToken { get; init; } = default!;

    /// <summary>
    /// access token 有效期（秒）.
    /// </summary>
    public int ExpiresIn { get; init; }

    /// <summary>
    /// token 类型，固定 Bearer.
    /// </summary>
    public string TokenType { get; init; } = "Bearer";

    /// <summary>
    /// MoAI 用户 id.
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    /// 用户名.
    /// </summary>
    public string UserName { get; init; } = default!;

    /// <summary>
    /// 昵称.
    /// </summary>
    public string? NickName { get; init; }

    /// <summary>
    /// 头像 ObjectKey（空=未设置）.
    /// </summary>
    public string? AvatarPath { get; init; }
}
