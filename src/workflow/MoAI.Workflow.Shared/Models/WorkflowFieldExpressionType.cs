using System.Text.Json.Serialization;

namespace MoAI.Workflow.Models;

public enum WorkflowFieldExpressionType
{
    /// <summary>
    /// 未设置或固定，即直接赋值.
    /// </summary>
    [JsonPropertyName("none")]
    Fixed,

    /// <summary>
    /// 使用变量赋值，支持系统变量、全局变量、节点变量等.
    /// </summary>
    [JsonPropertyName("variable")]
    Variable,

    /// <summary>
    /// JsonPath 表达式.
    /// </summary>
    [JsonPropertyName("jsonpath")]
    JsonPath,

    /// <summary>
    /// 字符串插值表达式，可使用变量插值，不能对数组、对象类型使用.
    /// </summary>
    [JsonPropertyName("string_interpolation")]
    StringInterpolation,

    /// <summary>
    /// 正则表达式，不能对数组、对象类型使用.
    /// </summary>
    [JsonPropertyName("regex")]
    Regex,
}
