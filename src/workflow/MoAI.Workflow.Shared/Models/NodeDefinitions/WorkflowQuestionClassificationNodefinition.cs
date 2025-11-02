namespace MoAI.Workflow.Models.NodeDefinitions;

/// <summary>
/// 问题分类器.
/// </summary>
public class WorkflowQuestionClassificationNodefinition : WorkflowNodefinition
{
    /// <summary>
    /// AI模型id.
    /// </summary>
    public int AiModelId { get; init; } = default!;

    /// <inheritdoc/>
    public override NodeType NodeType { get; protected init; } = NodeType.AiQuestion;

    /// <summary>
    /// 提示词.
    /// </summary>
    public string Prompt { get; init; } = default!;

    /// <summary>
    /// 用户问题.
    /// </summary>
    public WorkflowFieldDefinition Question { get; init; } = default!;

    /// <summary>
    /// 问题取值表达式.
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
