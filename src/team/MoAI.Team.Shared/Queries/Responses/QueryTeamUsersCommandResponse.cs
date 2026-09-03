namespace MoAI.Team.Queries.Responses;

/// <summary>
/// 团队成员列表响应.
/// </summary>
public class QueryTeamUsersCommandResponse
{
    /// <summary>
    /// 成员集合.
    /// </summary>
    public IReadOnlyList<TeamUserItem> Items { get; set; } = new List<TeamUserItem>();
}
