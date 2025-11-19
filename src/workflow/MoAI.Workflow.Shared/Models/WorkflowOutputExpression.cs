namespace MoAI.Workflow.Models;

public class WorkflowOutputExpression
{
    /// <summary>
    /// 节点输出取值表达式类型.
    /// </summary>
    public WorkflowOutputExpressionType Type { get; init; } = WorkflowOutputExpressionType.Ignore;

    /// <summary>
    /// 使用 JavaScript 脚本来生成输出.
    /// </summary>
    public string JavaScript { get; init; } = string.Empty;
}
