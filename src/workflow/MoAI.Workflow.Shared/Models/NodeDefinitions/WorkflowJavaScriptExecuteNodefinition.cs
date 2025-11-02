namespace MoAI.Workflow.Models.NodeDefinitions;

/// <summary>
/// JavaScript 执行器.
/// </summary>
public class WorkflowJavaScriptExecuteNodefinition : WorkflowNodefinition
{
    /// <inheritdoc/>
    public override NodeType NodeType { get; protected init; } = NodeType.JavaScript;

    /// <summary>
    /// 链接的节点列表.
    /// </summary>
    public Guid ConnectionNodeId { get; init; }

    /// <summary>
    /// 要执行的 JavaScript 脚本.
    /// </summary>
    public string JavaScript { get; init; } = string.Empty;

    /// <summary>
    /// 赋值表达式类型.
    /// </summary>
    public WorkflowFieldExpressionType InputExpressionType { get; init; } = WorkflowFieldExpressionType.Fixed;

    /// <summary>
    /// 赋值表达式.
    /// </summary>
    public string InputExpression { get; init; } = "function run((paramter, sys, g)) {}";

    /// <summary>
    /// 是否设置动态输出，如果是，则不需要填写 Output 表达式.
    /// </summary>
    public bool IsDynamic { get; init; } = true;

    /// <summary>
    /// 输出参数.
    /// </summary>
    public IReadOnlyCollection<WorkflowFieldDefinition> OutputFields { get; init; } = Array.Empty<WorkflowFieldDefinition>();

    /// <summary>
    /// 节点输出取值表达式类型，该节点只支持 Fixed、Dynamic.
    /// </summary>
    public WorkflowOutputExpressionType Type { get; init; } = WorkflowOutputExpressionType.Dynamic;
}