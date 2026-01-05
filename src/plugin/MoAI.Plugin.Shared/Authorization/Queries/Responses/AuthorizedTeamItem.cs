namespace MoAI.Plugin.Authorization.Queries.Responses;

/// <summary>
/// 授权团队信息项.
/// </summary>
public class AuthorizedTeamItem
{
    /// <summary>
    /// 团队ID.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 团队名称.
    /// </summary>
    public string TeamName { get; init; } = default!;

    /// <summary>
    /// 授权ID.
    /// </summary>
    public int AuthorizationId { get; init; }
}
