namespace MoAI.Team.Queries.Responses;

/// <summary>
/// 查询所有团队简单信息列表响应.
/// </summary>
public class QueryAllTeamSimpleListCommandResponse
{
    /// <summary>
    /// 团队列表.
    /// </summary>
    public IReadOnlyCollection<QueryAllTeamSimpleListCommandResponseItem> Items { get; init; } = Array.Empty<QueryAllTeamSimpleListCommandResponseItem>();
}
