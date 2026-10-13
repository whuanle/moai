namespace MoAI.Timer.Queries.Responses;

/// <summary>
/// 团队定时器执行历史响应.
/// </summary>
public class QueryTeamTimerExecutionsCommandResponse
{
    /// <summary>
    /// 总数.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 执行记录列表.
    /// </summary>
    public IReadOnlyList<TeamTimerExecutionItem> Items { get; set; } = [];
}
