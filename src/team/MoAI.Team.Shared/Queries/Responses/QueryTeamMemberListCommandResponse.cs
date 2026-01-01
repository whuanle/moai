namespace MoAI.Team.Queries.Responses;

/// <summary>
/// 团队成员列表响应.
/// </summary>
public class QueryTeamMemberListCommandResponse
{
    /// <summary>
    /// 成员列表.
    /// </summary>
    public List<QueryTeamMemberListQueryResponseItem> Items { get; init; } = new();
}
