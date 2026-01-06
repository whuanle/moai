namespace MoAI.Team.Queries.Responses;

/// <summary>
/// 团队简单信息项.
/// </summary>
public class QueryAllTeamSimpleListCommandResponseItem
{
    /// <summary>
    /// 团队ID.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// 团队名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 团队描述.
    /// </summary>
    public string Description { get; init; } = default!;

    /// <summary>
    /// 所有者ID.
    /// </summary>
    public int OwnerId { get; init; }

    /// <summary>
    /// 所有者名称.
    /// </summary>
    public string OwnerName { get; init; } = default!;
}
