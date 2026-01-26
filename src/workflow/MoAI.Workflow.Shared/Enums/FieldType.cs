namespace MoAI.Workflow.Enums;

/// <summary>
/// 字段数据类型枚举.
/// </summary>
public enum FieldType
{
    /// <summary>
    /// 空类型 - 无数据.
    /// </summary>
    Empty,

    /// <summary>
    /// 字符串类型.
    /// </summary>
    String,

    /// <summary>
    /// 数字类型（整数或浮点数）.
    /// </summary>
    Number,

    /// <summary>
    /// 布尔类型（true 或 false）.
    /// </summary>
    Boolean,

    /// <summary>
    /// 对象类型 - 复杂的 JSON 对象.
    /// </summary>
    Object,

    /// <summary>
    /// 数组类型 - 元素集合.
    /// </summary>
    Array,

    /// <summary>
    /// 动态类型 - 运行时确定的类型.
    /// </summary>
    Dynamic,
}
