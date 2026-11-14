using System;
using System.Collections.Generic;

namespace MoAI.Statistics.Queries.Responses;

/// <summary>
/// 资源使用趋势（按日聚合）响应.
/// </summary>
public class QueryResourceUsageTrendCommandResponse
{
    /// <summary>
    /// 按日序列（区间内无数据的日期补 0，升序）.
    /// </summary>
    public IReadOnlyList<ResourceUsageTrendItem> Items { get; set; } = new List<ResourceUsageTrendItem>();

    /// <summary>
    /// 区间合计.
    /// </summary>
    public long Total { get; set; }

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
public class ResourceUsageTrendItem
{
    /// <summary>
    /// 统计日期.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// 当日合计（按小时粒度落库的自动聚合到日）.
    /// </summary>
    public long Count { get; set; }
}
