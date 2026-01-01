namespace MoAI.Team.Queries.Responses;

/// <summary>
/// 查询团队列表响应.
/// </summary>
public class QueryTeamListCommandResponse
{
    /// <summary>
    /// 团队列表.
    /// </summary>
    public IReadOnlyCollection<QueryTeamListQueryResponseItem> Items { get; init; } = Array.Empty<QueryTeamListQueryResponseItem>();
}
