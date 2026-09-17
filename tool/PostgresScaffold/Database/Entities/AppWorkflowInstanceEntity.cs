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
/// 流程应用运行实例，一次工作流执行的完整快照（含节点级状态，支撑断点恢复）.
/// </summary>
public partial class AppWorkflowInstanceEntity : IFullAudited
{
    public Guid Id { get; set; }

    public int TeamId { get; set; }

    public Guid AppId { get; set; }

    public Guid WorkflowConfigId { get; set; }

    /// <summary>
    /// 执行引用的定义版本号，0=调试执行（运行草稿定义）.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// 是否调试运行（设计器内发起），0=正式执行 1=调试.
    /// </summary>
    public bool IsDebug { get; set; }

    /// <summary>
    /// 实例状态，0=已创建 1=执行中 2=已挂起 3=已完成 4=已取消.
    /// </summary>
    public short Status { get; set; }

    public string Input { get; set; } = default!;

    public string? Output { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 引擎实例全量 JSON（WorkflowInstance 序列化，含每个节点的状态/输入/输出/耗时），实例自含定义快照.
    /// </summary>
    public string InstanceData { get; set; } = default!;

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public long CreateUserId { get; set; }

    public DateTimeOffset CreateTime { get; set; }

    public long UpdateUserId { get; set; }

    public DateTimeOffset UpdateTime { get; set; }

    public long IsDeleted { get; set; }
}
