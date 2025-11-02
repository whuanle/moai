namespace MoAI.Workflow.Models;

/// <summary>
/// 字段赋值表达式类型.
/// </summary>
public class WorkflowFieldValuationExpression
{
    /// <summary>
    /// 取值表达式类型.
    /// </summary>
    public WorkflowFieldExpressionType ExpressionType { get; init; } = WorkflowFieldExpressionType.Fixed;

    /// <summary>
    /// 表达式的值.
    /// </summary>
    public string ExpressionValue { get; init; } = string.Empty;
}
