using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 流程设计实例表.
/// </summary>
public partial class AppWorkflowDesignEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid Id { get; set; } = default!;

    /// <summary>
    /// 团队id.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 应用id.
    /// </summary>
    public Guid AppId { get; set; } = default!;

    /// <summary>
    /// ui设计，存储的是发布版本.
    /// </summary>
    public string UiDesign { get; set; } = default!;

    /// <summary>
    /// 功能设计，存储的是发布版本.
    /// </summary>
    public string FunctionDesgin { get; set; } = default!;

    /// <summary>
    /// ui设计草稿.
    /// </summary>
    public string UiDesignDraft { get; set; } = default!;

    /// <summary>
    /// 功能设计草稿.
    /// </summary>
    public string FunctionDesignDraft { get; set; } = default!;

    /// <summary>
    /// 是否发布.
    /// </summary>
    public bool IsPublish { get; set; }

    /// <summary>
    /// 创建人.
    /// </summary>
    public int CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新人.
    /// </summary>
    public int UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
