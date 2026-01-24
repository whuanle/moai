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
/// 流程设计快照，每次发布都会留下快照.
/// </summary>
public partial class WorkflowDesginSnapshootEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid Id { get; set; } = default!;

    /// <summary>
    /// 流程id.
    /// </summary>
    public Guid WorkflowDesginId { get; set; } = default!;

    /// <summary>
    /// 团队id.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// ui设计.
    /// </summary>
    public string UiDesign { get; set; } = default!;

    /// <summary>
    /// 功能设计.
    /// </summary>
    public string FunctionDesign { get; set; } = default!;

    /// <summary>
    /// 版本.
    /// </summary>
    public string Version { get; set; } = default!;
}
