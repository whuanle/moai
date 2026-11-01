using System;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 资源使用统计（总量），统计模块定期批量落库：按 资源类型 name + 资源 id 累计总量，不按日隔离.
/// </summary>
public partial class ResourceUsageTotalEntity
{
    /// <summary>
    /// id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 资源类型名，如 plugin-usage / app-usage / skill-usage.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 资源 id 文本，各资源主键形态不一（Guid/int/key），统一以字符串存储.
    /// </summary>
    public string ResourceId { get; set; } = default!;

    /// <summary>
    /// 累计总量.
    /// </summary>
    public long Total { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }
}
