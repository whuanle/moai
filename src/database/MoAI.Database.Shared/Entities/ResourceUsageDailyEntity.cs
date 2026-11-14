using System;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 资源使用统计（每日），统计模块定期批量落库：按 资源类型 name + 资源 id + 日期 记录当日累计量.
/// </summary>
public partial class ResourceUsageDailyEntity
{
    /// <summary>
    /// id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 统计日期（UTC 日）.
    /// </summary>
    public DateOnly UsageDate { get; set; }

    /// <summary>
    /// 统计小时（UTC，0~23）；-1 表示按天粒度（整日一个桶）.
    /// </summary>
    public int UsageHour { get; set; } = -1;

    /// <summary>
    /// 资源类型名，如 plugin-usage / app-usage / skill-usage.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 资源 id 文本，各资源主键形态不一（Guid/int/key），统一以字符串存储.
    /// </summary>
    public string ResourceId { get; set; } = default!;

    /// <summary>
    /// 当日累计数量.
    /// </summary>
    public long Count { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }
}
