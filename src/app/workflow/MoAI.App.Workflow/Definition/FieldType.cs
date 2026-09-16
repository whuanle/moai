using System.Text.Json.Serialization;

namespace MoAI.App.Workflow.Definition;

/// <summary>
/// 字段数据类型枚举.
/// </summary>
public enum FieldType
{
    /// <summary>
    /// 字符串.
    /// </summary>
    String,

    /// <summary>
    /// 数字.
    /// </summary>
    Number,

    /// <summary>
    /// 布尔.
    /// </summary>
    Boolean,

    /// <summary>
    /// JSON 对象.
    /// </summary>
    Object,

    /// <summary>
    /// 字典（字段名不确定的对象）.
    /// </summary>
    Map,

    /// <summary>
    /// 数组.
    /// </summary>
    Array,

    /// <summary>
    /// 动态类型，运行时确定.
    /// </summary>
    Dynamic,
}
