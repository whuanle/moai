namespace MoAI.Workflow.Models.NodeDefinitions;

/// <summary>
/// 节点抽象.
/// </summary>
public abstract class WorkflowNodefinition
{
    /// <summary>
    /// guid.
    /// </summary>
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>
    /// 名字.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 节点类型.
    /// </summary>
    public virtual NodeType NodeType { get; protected init; }
}
