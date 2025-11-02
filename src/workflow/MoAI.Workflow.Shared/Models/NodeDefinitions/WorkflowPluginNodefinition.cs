namespace MoAI.Workflow.Models.NodeDefinitions;

/// <summary>
/// 手动调用插件节点定义，固定一个输出变量 body.
/// </summary>
public class WorkflowPluginNodefinition : WorkflowNodefinition
{
    /// <summary>
    /// 插件id.
    /// </summary>
    public int PluginId { get; init; } = default!;

    /// <summary>
    /// 要调用的插件的 Action 名称.
    /// </summary>
    public string Action { get; init; } = default!;

    /// <inheritdoc/>
    public override NodeType NodeType { get; protected init; } = NodeType.Plugin;

    /// <summary>
    /// Header 参数.
    /// </summary>
    public IReadOnlyCollection<WorkflowFieldDefinition> Header { get; init; } = Array.Empty<WorkflowFieldDefinition>();

    /// <summary>
    /// 输入参数.
    /// </summary>
    public IReadOnlyCollection<WorkflowFieldDefinition> Input { get; init; } = Array.Empty<WorkflowFieldDefinition>();

    /// <summary>
    /// Header 取值的表达式.
    /// </summary>
    public WorkflowOutputExpression HeaderExpression { get; init; } = new WorkflowOutputExpression();

    /*
     固定输出变量 body，表示插件的返回值.
     */

    /// <summary>
    /// 输出参数.
    /// </summary>
    public IReadOnlyCollection<WorkflowFieldDefinition> Output { get; init; } = Array.Empty<WorkflowFieldDefinition>();

    /// <summary>
    /// 生成输出的表达式.
    /// </summary>
    public WorkflowOutputExpression OutputExpression { get; init; } = new WorkflowOutputExpression();
}
