using System;
using MoAI.Database.Audits;

namespace MoAI.Database.Entities;

/// <summary>
/// 知识图谱关系类型（schema）.
/// </summary>
public partial class KnowledgeGraphRelationTypeEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 所属图谱 id.
    /// </summary>
    public long KgId { get; set; }

    /// <summary>
    /// 关系类型名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 颜色（展示用）.
    /// </summary>
    public string Color { get; set; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 允许的起点实体类型 id，null=任意.
    /// </summary>
    public long? SourceTypeId { get; set; }

    /// <summary>
    /// 允许的终点实体类型 id，null=任意.
    /// </summary>
    public long? TargetTypeId { get; set; }

    /// <summary>
    /// 排序.
    /// </summary>
    public int Sort { get; set; }

    /// <summary>
    /// 创建人.
    /// </summary>
    public long CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 最后修改人.
    /// </summary>
    public long UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
