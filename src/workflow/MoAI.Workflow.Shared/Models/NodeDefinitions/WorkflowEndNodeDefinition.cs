namespace MoAI.Workflow.Models.NodeDefinitions;

public class WorkflowEndNodeDefinition : WorkflowNodefinition
{
    /// <inheritdoc/>
    public override NodeType NodeType { get; protected init; } = NodeType.End;

    /// <summary>
    /// 输入参数.
    /// </summary>
    public IReadOnlyCollection<WorkflowFieldDefinition> Input { get; init; } = Array.Empty<WorkflowFieldDefinition>();

    /// <summary>
    /// 输出参数.
    /// </summary>
    public IReadOnlyCollection<WorkflowFieldDefinition> Output { get; init; } = Array.Empty<WorkflowFieldDefinition>();

    /// <summary>
    /// 生成输出的表达式.
    /// </summary>
    public WorkflowOutputExpression OutputExpression { get; init; } = new WorkflowOutputExpression();
}