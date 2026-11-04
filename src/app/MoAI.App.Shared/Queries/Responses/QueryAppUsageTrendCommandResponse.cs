namespace MoAI.App.Queries.Responses;

/// <summary>
/// 应用使用趋势（按日聚合）响应.
/// </summary>
public class QueryAppUsageTrendCommandResponse
{
    /// <summary>
    /// 按日序列（区间内无数据的日期补 0，升序）.
    /// </summary>
    public IReadOnlyList<AppUsageTrendItem> Items { get; set; } = new List<AppUsageTrendItem>();

    /// <summary>
    /// 实际统计起始日期.
    /// </summary>
    public DateOnly StartDate { get; set; }

    /// <summary>
    /// 实际统计结束日期.
    /// </summary>
    public DateOnly EndDate { get; set; }
}

/// <summary>
/// 趋势单日数据.
/// </summary>
public class AppUsageTrendItem
{
    /// <summary>
    /// 统计日期.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// 当日使用次数（会话创建计数，按小时粒度落库的自动聚合到日）.
    /// </summary>
    public long UseCount { get; set; }

    /// <summary>
    /// 当日消息条数（会话落库消息行数增量累计，可为负日回减）.
    /// </summary>
    public long MessageCount { get; set; }
}
