namespace MoAI.Team.Queries.Responses;

/// <summary>
/// 全部团队分页查询结果（管理员）.
/// </summary>
public class QueryAdminTeamListCommandResponse
{
    /// <summary>
    /// 总数量.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// 团队列表.
    /// </summary>
    public IReadOnlyList<QueryAdminTeamListCommandResponseItem> Items { get; init; } = Array.Empty<QueryAdminTeamListCommandResponseItem>();
}
