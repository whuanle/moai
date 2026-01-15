namespace MoAI.Wiki.Plugins.Template.Queries;

/// <summary>
/// 查询定时任务信息响应.
/// </summary>
public class QueryRecurringJobCommandResponse
{
    /// <summary>
    /// 任务在不在.
    /// </summary>
    public bool IsExist { get; set; }

    /// <summary>
    /// 时间表达式.
    /// </summary>
    public string? Cron { get; set; } = default!;

    /// <summary>
    /// 执行异常信息.
    /// </summary>
    public string? LoadException { get; set; } = default!;

    /// <summary>
    /// 下次执行时间.
    /// </summary>
    public DateTimeOffset? NextExecution { get; set; }

    /// <summary>
    /// 最后执行的任务状态.
    /// </summary>
    public string? LastJobState { get; set; } = default!;

    /// <summary>
    /// 上一次执行的时间.
    /// </summary>
    public DateTimeOffset? LastExecution { get; set; }

    /// <summary>
    /// 错误信息.
    /// </summary>
    public string? Error { get; set; } = default!;
}