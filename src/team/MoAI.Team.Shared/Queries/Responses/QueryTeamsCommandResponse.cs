namespace MoAI.Team.Queries.Responses;

/// <summary>
/// 团队列表响应.
/// </summary>
public class QueryTeamsCommandResponse
{
    /// <summary>
    /// 团队集合.
    /// </summary>
    public IReadOnlyList<TeamItem> Items { get; set; } = new List<TeamItem>();
}
