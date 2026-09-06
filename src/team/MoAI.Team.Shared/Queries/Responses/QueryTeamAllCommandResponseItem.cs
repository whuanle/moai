namespace MoAI.Team.Queries.Responses;

/// <summary>
/// 全部团队列表项（管理员）.
/// </summary>
public class QueryTeamAllCommandResponseItem
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; set; }

    /// <summary>
    /// 团队名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 是否禁用.
    /// </summary>
    public bool IsDisable { get; set; }

    /// <summary>
    /// 成员数量.
    /// </summary>
    public int MemberCount { get; set; }
}
