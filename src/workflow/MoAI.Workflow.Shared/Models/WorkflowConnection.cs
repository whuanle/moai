namespace MoAI.Workflow.Models;

public class WorkflowConnection
{
    /// <summary>
    /// 当前节点.
    /// </summary>
    public Guid FromNodeId { get; init; }

    /// <summary>
    /// 上一个节点.
    /// </summary>
    public Guid LastNodeId { get; init; }

    /// <summary>
    /// 结束节点.
    /// </summary>
    public required List<Guid> ToNodeIds { get; init; }
}