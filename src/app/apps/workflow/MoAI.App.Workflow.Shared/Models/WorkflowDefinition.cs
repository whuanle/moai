namespace MoAI.Workflow.Models;

/// <summary>
/// 工作流定义模型，包含完整的工作流设计信息.
/// </summary>
public class WorkflowDefinition
{
    /// <summary>
    /// 工作流定义的唯一标识符.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 工作流名称.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 工作流描述信息.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 节点列表，包含所有节点的配置信息.
    /// </summary>
    public IReadOnlyCollection<NodeDesign> Nodes { get; set; } = Array.Empty<NodeDesign>();

    /// <summary>
    /// 连接列表，定义节点之间的连接关系.
    /// </summary>
    public IReadOnlyCollection<Connection> Connections { get; set; } = Array.Empty<Connection>();
}
