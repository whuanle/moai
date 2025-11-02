namespace MoAI.Workflow.Models.NodeDefinitions;

/// <summary>
/// 开始节点定义.
/// </summary>
public class WorkflowStartNodeDefinition : WorkflowNodefinition
{
    /// <inheritdoc/>
    public override NodeType NodeType { get; protected init; } = NodeType.Start;

    /// <summary>
    /// 输入参数，开始节点的输入参数会自动赋值或生成全局变量.
    /// </summary>
    public IReadOnlyCollection<WorkflowFieldDefinition> Input { get; init; } = Array.Empty<WorkflowFieldDefinition>();

    /// <summary>
    /// 节点输出取值表达式类型，支持 Fixed、Dynamic、JavaScript.
    /// </summary>
    public WorkflowOutputExpressionType OutputType { get; init; } = WorkflowOutputExpressionType.Fixed;

    /// <summary>
    /// 输出参数.
    /// </summary>
    public IReadOnlyCollection<WorkflowFieldDefinition> OutputFields { get; init; } = Array.Empty<WorkflowFieldDefinition>();

    /// <summary>
    /// 要执行的 JavaScript 代码，返回的代码就是输出参数.
    /// </summary>
    public string JavaScript { get; init; } = "function run((paramter, sys, g)) { return paramter;}";

    /// <summary>
    /// 链接的节点列表.
    /// </summary>
    public Guid ConnectionNodeId { get; init; }
}
