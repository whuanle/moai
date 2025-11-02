namespace MoAI.Workflow.Models;

public class WorkflowBaseFieldDefinition
{
    /// <summary>
    /// 字段显示名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 变量名称，只能使用字母、数字和下划线，不能以数字开头，不能包含空格.<br />
    /// 嵌套字段格式："Var1.Var2"
    /// </summary>
    public string Key { get; set; } = default!;

    /// <summary>
    /// 字段类型.
    /// </summary>
    public WorkflowFieldType Type { get; set; }

    /// <summary>
    /// 默认值，以 json 字符串存储，使用是动态转换为对应的类型.
    /// </summary>
    public string Default { get; init; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = default!;

    /// <summary>
    /// 排序.
    /// </summary>
    public int Index { get; init; }
}
