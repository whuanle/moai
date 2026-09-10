using System;
using MoAI.Database.Audits;

namespace MoAI.Database.Entities;

/// <summary>
/// 知识图谱.
/// </summary>
public partial class KnowledgeGraphEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 所属团队 id.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 简介.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 创建时使用的模板 key，null 表示自定义.
    /// </summary>
    public string? TemplateKey { get; set; }

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
