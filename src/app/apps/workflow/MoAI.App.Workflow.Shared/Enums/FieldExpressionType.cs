namespace MoAI.Workflow.Enums;

/// <summary>
/// 字段表达式类型枚举 - 定义如何为节点输入分配值.
/// </summary>
public enum FieldExpressionType
{
    /// <summary>
    /// 固定值 - 常数值，不进行任何解析.
    /// </summary>
    Fixed,

    /// <summary>
    /// 变量引用 - 引用工作流上下文中的变量（sys.*、input.*、nodeKey.*）.
    /// </summary>
    Variable,

    /// <summary>
    /// JSON 路径表达式 - 使用点符号访问嵌套对象（例如：nodeA.result[0].name）.
    /// </summary>
    JsonPath,

    /// <summary>
    /// 字符串插值 - 模板字符串，支持变量替换（例如：Hello {input.name}）.
    /// </summary>
    StringInterpolation,
}
