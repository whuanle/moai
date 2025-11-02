namespace MoAI.Workflow.Engines;

/// <summary>
/// 流程节点输出结果.
/// </summary>
public interface IWorkflowNodeRunResult
{
    /// <summary>
    /// 被执行的节点的定义.
    /// </summary>
    public Guid DefinitionId { get; }

    /// <summary>
    /// 被执行的实例的 Id.
    /// </summary>
    public Guid InstanceId { get; }

    /// <summary>
    /// 输入值.
    /// </summary>
    public string InputJson { get; }

    /// <summary>
    /// 输出值.
    /// </summary>
    public string OutputJson { get; }

    /// <summary>
    /// 启动时间.
    /// </summary>
    public DateTimeOffset StartTime { get; }

    /// <summary>
    /// 结束时间.
    /// </summary>
    public DateTimeOffset EndTime { get; }

    /// <summary>
    /// 当前运行状态.
    /// </summary>
    public WorkflowNodeRunStatus Status { get; }

    /// <summary>
    /// 错误信息.
    /// </summary>
    public string ErrorMessage { get; }

    /// <summary>
    /// 上一个节点定义 id.
    /// </summary>
    public Guid LastNodeDefinitionId { get; }

    /// <summary>
    /// 上一个节点实例 id.
    /// </summary>
    public Guid LastNodeInstanceId { get; }

    /// <summary>
    /// 下一个节点定义 id.
    /// </summary>
    public Guid NextNodeDefinitionId { get; }

    /// <summary>
    /// 下一个节点实例 id.
    /// </summary>
    public Guid NextNodeInstanceId { get; }
}
