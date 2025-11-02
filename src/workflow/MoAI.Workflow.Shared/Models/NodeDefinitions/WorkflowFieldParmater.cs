namespace MoAI.Workflow.Models.NodeDefinitions;

public class WorkflowFieldParmater
{
    /// <summary>
    /// 字段名称.
    /// </summary>
    public string Key { get; init; } = default!;

    /// <summary>
    /// 字段描述.
    /// </summary>
    public string Description { get; init; } = default!;

    /// <summary>
    /// 字段类型.
    /// </summary>
    public WorkflowFieldType Type { get; init; } = default!;

    /// <summary>
    /// 子类型.
    /// </summary>
    public IReadOnlyCollection<WorkflowFieldParmater> Items { get; init; } = new List<WorkflowFieldParmater>();
}