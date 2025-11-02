namespace MoAI.Workflow.Models.NodeDefinitions;

/// <summary>
/// 发起 http 请求.
/// </summary>
public class WorkflowHttpNodefinition : WorkflowNodefinition
{
    /// <inheritdoc/>
    public override NodeType NodeType { get; protected init; } = NodeType.Http;

    /// <summary>
    /// 输出参数.
    /// </summary>
    public IReadOnlyCollection<WorkflowFieldDefinition> Output { get; init; } = Array.Empty<WorkflowFieldDefinition>();

    /// <summary>
    /// 生成输出的表达式.
    /// </summary>
    public WorkflowOutputExpression OutputExpression { get; init; } = new WorkflowOutputExpression();
}
