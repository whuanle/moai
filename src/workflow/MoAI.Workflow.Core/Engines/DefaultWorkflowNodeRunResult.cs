namespace MoAI.Workflow.Engines;

public class DefaultWorkflowNodeRunResult : IWorkflowNodeRunResult
{
    /// <summary>
    /// 被执行的节点的定义.
    /// </summary>
    public virtual Guid DefinitionId { get; set; }

    /// <summary>
    /// 被执行的实例的 Id.
    /// </summary>
    public virtual Guid InstanceId { get; set; }

    /// <summary>
    /// 输入值.
    /// </summary>
    public virtual string InputJson { get; set; } = string.Empty!;

    /// <summary>
    /// 输出值.
    /// </summary>
    public virtual string OutputJson { get; set; } = string.Empty!;

    /// <summary>
    /// 启动时间.
    /// </summary>
    public virtual DateTimeOffset StartTime { get; set; } = DateTimeOffset.Now;

    /// <summary>
    /// 结束时间.
    /// </summary>
    public virtual DateTimeOffset EndTime { get; set; } = default!;

    /// <summary>
    /// 当前运行状态.
    /// </summary>
    public virtual WorkflowNodeRunStatus Status { get; set; } = WorkflowNodeRunStatus.Success;

    /// <summary>
    /// 错误信息.
    /// </summary>
    public virtual string ErrorMessage { get; set; } = string.Empty!;

    /// <summary>
    /// 上一个节点定义 id.
    /// </summary>
    public virtual Guid LastNodeDefinitionId { get; set; } = default!;

    /// <summary>
    /// 上一个节点实例 id.
    /// </summary>
    public virtual Guid LastNodeInstanceId { get; set; } = default!;

    /// <summary>
    /// 下一个节点定义 id.
    /// </summary>
    public virtual Guid NextNodeDefinitionId { get; set; } = default!;

    /// <summary>
    /// 下一个节点实例 id.
    /// </summary>
    public virtual Guid NextNodeInstanceId { get; set; } = default!;
}