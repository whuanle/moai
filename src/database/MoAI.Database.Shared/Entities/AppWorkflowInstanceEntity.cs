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
    /// <summary>
    /// 实例ID.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 所属团队ID，逻辑关联app.team_id，冗余用于团队维度过滤.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 所属应用ID，逻辑关联app.id.
    /// </summary>
    public Guid AppId { get; set; }

    /// <summary>
    /// 编排配置ID，逻辑关联app_workflow_config.id.
    /// </summary>
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

    /// <summary>
    /// 启动参数 JSON 对象文本，空为 &apos;{}&apos;.
    /// </summary>
    public string Input { get; set; } = default!;

    /// <summary>
    /// 最终输出 JSON 对象文本，未产出为 null.
    /// </summary>
    public string? Output { get; set; }

    /// <summary>
    /// 失败/挂起原因，无异常为 null.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 引擎实例全量 JSON（WorkflowInstance 序列化，含每个节点的状态/输入/输出/耗时），实例自含定义快照.
    /// </summary>
    public string InstanceData { get; set; } = default!;

    /// <summary>
    /// 开始执行时间.
    /// </summary>
    public DateTimeOffset? StartTime { get; set; }

    /// <summary>
    /// 结束时间（完成/挂起/取消）.
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>
    /// 创建人.
    /// </summary>
    public long CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新人.
    /// </summary>
    public long UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除，0=未删除（legacy bigint 约定）.
    /// </summary>
    public long IsDeleted { get; set; }
}
