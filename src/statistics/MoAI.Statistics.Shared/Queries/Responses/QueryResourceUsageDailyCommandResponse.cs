using System;
using System.Collections.Generic;

namespace MoAI.Statistics.Queries.Responses;

/// <summary>
/// 资源使用统计（每日）分页响应.
/// </summary>
public class QueryResourceUsageDailyCommandResponse
{
    /// <summary>
    /// 记录集合.
    /// </summary>
    public IReadOnlyList<ResourceUsageDailyItem> Items { get; set; } = new List<ResourceUsageDailyItem>();

    /// <summary>
    /// 记录总数.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// 页码（从 1 开始）.
    /// </summary>
    public int PageNo { get; set; }

    /// <summary>
    /// 每页大小.
    /// </summary>
    public int PageSize { get; set; }
}

/// <summary>
/// 资源使用统计（每日）记录.
/// </summary>
public class ResourceUsageDailyItem
{
    /// <summary>
    /// 记录 id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 统计日期.
    /// </summary>
    public DateOnly UsageDate { get; set; }

    /// <summary>
    /// 统计小时（UTC，0~23）；-1 表示按天粒度（整日一个桶）.
    /// </summary>
    public int UsageHour { get; set; }

    /// <summary>
    /// 资源类型名.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 资源 id 文本.
    /// </summary>
    public string ResourceId { get; set; } = default!;

    /// <summary>
    /// 当日累计数量.
    /// </summary>
    public long Count { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }
}
