namespace MoAI.Timer.Queries.Responses;

/// <summary>
/// 团队定时器列表响应.
/// </summary>
public class QueryTeamTimersCommandResponse
{
    /// <summary>
    /// 总数.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 定时器列表.
    /// </summary>
    public IReadOnlyList<TeamTimerItem> Items { get; set; } = [];
}
