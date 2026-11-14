using System;
using System.Collections.Generic;

namespace MoAI.Statistics.Queries.Responses;

/// <summary>
/// 资源使用统计（总量）分页响应.
/// </summary>
public class QueryResourceUsageTotalCommandResponse
{
    /// <summary>
    /// 记录集合.
    /// </summary>
    public IReadOnlyList<ResourceUsageTotalItem> Items { get; set; } = new List<ResourceUsageTotalItem>();

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
/// 资源使用统计（总量）记录.
/// </summary>
public class ResourceUsageTotalItem
{
    /// <summary>
    /// 记录 id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 资源类型名.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 资源 id 文本.
    /// </summary>
    public string ResourceId { get; set; } = default!;

    /// <summary>
    /// 累计总量.
    /// </summary>
    public long Total { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }
}
