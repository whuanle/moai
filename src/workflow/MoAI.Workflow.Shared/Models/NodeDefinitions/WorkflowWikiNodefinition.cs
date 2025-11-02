namespace MoAI.Workflow.Models.NodeDefinitions;

/// <summary>
/// 知识库搜索.
/// </summary>
public class WorkflowWikiNodefinition : WorkflowNodefinition
{
    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; init; } = default!;

    /// <inheritdoc/>
    public override NodeType NodeType { get; protected init; } = NodeType.Wiki;

    /// <summary>
    /// 用户问题.
    /// </summary>
    public WorkflowFieldDefinition Question { get; init; } = default!;

    /// <summary>
    /// 问题取值的表达式.
    /// </summary>
    public WorkflowFieldValuationExpression QuestionExpression { get; init; } = new WorkflowFieldValuationExpression();

    /// <summary>
    /// 输出参数.
    /// </summary>
    public IReadOnlyCollection<WorkflowFieldDefinition> Output { get; init; } = Array.Empty<WorkflowFieldDefinition>();

    /// <summary>
    /// 生成输出的表达式.
    /// </summary>
    public WorkflowOutputExpression OutputExpression { get; init; } = new WorkflowOutputExpression();
}
