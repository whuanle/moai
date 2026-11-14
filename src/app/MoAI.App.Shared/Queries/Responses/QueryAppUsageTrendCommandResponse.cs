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
    /// 各模型 Token 消耗趋势序列（按区间合计 token 降序，最多 8 条；每条覆盖完整日期区间，无数据为空列表）.
    /// </summary>
    public IReadOnlyList<AppUsageModelTokenSeries> ModelTokenSeries { get; set; } = new List<AppUsageModelTokenSeries>();

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

/// <summary>
/// 单模型 Token 消耗趋势序列.
/// </summary>
public class AppUsageModelTokenSeries
{
    /// <summary>
    /// 模型 id.
    /// </summary>
    public Guid ModelId { get; set; }

    /// <summary>
    /// 模型名称（模型已删除时回落为 id 字符串）.
    /// </summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// 逐日 token 数（覆盖完整统计区间，无数据日补 0，升序）.
    /// </summary>
    public IReadOnlyList<AppUsageModelTokenPoint> Points { get; set; } = new List<AppUsageModelTokenPoint>();
}

/// <summary>
/// 单模型单日 token 数.
/// </summary>
public class AppUsageModelTokenPoint
{
    /// <summary>
    /// 统计日期.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// 当日合计 token（该应用该模型全部用户聚合）.
    /// </summary>
    public long Tokens { get; set; }
}
