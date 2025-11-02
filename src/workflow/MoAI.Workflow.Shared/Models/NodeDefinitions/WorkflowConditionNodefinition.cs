namespace MoAI.Workflow.Models.NodeDefinitions;

/// <summary>
/// 条件分支.
/// </summary>
public class WorkflowConditionNodefinition : WorkflowNodefinition
{
    /// <inheritdoc/>
    public override NodeType NodeType { get; protected init; } = NodeType.Condition;

    public required WorkflowConditionBranch IfBranch { get; init; }

    public required List<WorkflowConditionBranch> ElseIfBranch { get; init; }

    /// <summary>
    /// 链接的节点列表.
    /// </summary>
    public Guid ElseConnectionNodeId { get; init; }

    /// <summary>
    /// 输出参数.
    /// </summary>
    public IReadOnlyCollection<WorkflowFieldDefinition> Output { get; init; } = Array.Empty<WorkflowFieldDefinition>();

    /// <summary>
    /// 生成输出的表达式.
    /// </summary>
    public WorkflowOutputExpression OutputExpression { get; init; } = new WorkflowOutputExpression();
}

// if 分支逻辑
// 多个 else if
// 一个 else
public class WorkflowConditionBranch
{
    /// <summary>
    /// 条件表达式，只能使用 RuleEngines 的规则.
    /// </summary>
    public string ConditionExpression { get; set; } = default!;

    /// <summary>
    /// 链接的节点列表.
    /// </summary>
    public Guid ElseConnectionNodeId { get; init; }
}