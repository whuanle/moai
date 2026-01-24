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
public partial class WorkflowDesignEntity : IFullAudited
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
    /// 流程名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 头像key.
    /// </summary>
    public string Avatar { get; set; } = default!;

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
}
