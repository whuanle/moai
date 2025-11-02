namespace MoAI.Workflow.Models;

/// <summary>
/// 节点字段，如果字段是数组类型，则只支持 JsonPath 对字段取值以及 JavaScript，不支持子层.
/// </summary>
public class WorkflowFieldDefinition : WorkflowBaseFieldDefinition
{
    /// <summary>
    /// 是否必须.
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// 赋值规则.
    /// </summary>
    public WorkflowFieldValuationExpression Expression { get; init; } = new WorkflowFieldValuationExpression();

    /// <summary>
    /// 子类型，如果读取字段是数组时需要填写，如果不是则不需要.
    /// </summary>
    public WorkflowFieldType ArrayChildrenType { get; init; }

    /// <summary>
    /// 子层.
    /// </summary>
    public IReadOnlyCollection<WorkflowFieldDefinition> Children { get; init; } = new List<WorkflowFieldDefinition>();
}

public class WorkflowFieldInstance
{
    /// <summary>
    /// 字段显示名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 变量名称，只能使用字母、数字和下划线，不能以数字开头，不能包含空格.
    /// </summary>
    public string Key { get; set; } = default!;

    /// <summary>
    /// 字段类型.
    /// </summary>
    public WorkflowFieldType Type { get; set; }

    /// <summary>
    /// 字段值.
    /// </summary>
    public object? Value { get; set; }

    public List<WorkflowFieldInstance> Items { get; set; } = new List<WorkflowFieldInstance>();
}

// 每个节点的输入都是 json，全部转化为 json 字段，结构全部铺开
public class WorkflowNodeResult
{
    /// <summary>
    /// 节点实例 id.
    /// </summary>
    public Guid InstanceId { get; init; } = Guid.CreateVersion7();

    /// <summary>
    /// 定义 id.
    /// </summary>
    public Guid DefinitionId { get; init; } = Guid.CreateVersion7();

    ///// <summary>
    ///// 输出字段.
    ///// </summary>
    // public IReadOnlyCollection<WorkflowFieldInstance> Output { get; init; } = new List<WorkflowFieldInstance>();

    /// <summary>
    /// 输出对象.
    /// </summary>
    public string OutputJson { get; init; } = string.Empty;
}