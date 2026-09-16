using System.Text.Json.Serialization;

namespace MoAI.App.Workflow.Definition;

/// <summary>
/// 字段表达式类型枚举 - 定义如何为节点输入字段分配值（流程数据传输的核心契约）.
/// </summary>
public enum ExpressionType
{
    /// <summary>
    /// 运行时传入，只能用于开始节点的输出定义（工作流启动参数）.
    /// </summary>
    [JsonStringEnumMemberName("run")]
    Run,

    /// <summary>
    /// 固定值 - 常量，不做变量解析（数字/布尔会按 JSON 字面量解析）.
    /// </summary>
    [JsonStringEnumMemberName("fixed")]
    Fixed,

    /// <summary>
    /// 变量引用 - 引用上下文变量（sys.*、input.*、nodeKey.field），支持数组下标 [0] 和通配 [*].
    /// </summary>
    [JsonStringEnumMemberName("variable")]
    Variable,

    /// <summary>
    /// JSON 路径 - 在 {sys, input, nodes} 上下文上执行 JsonPath 查询，如 $.nodes.search.documents[*].title.
    /// </summary>
    [JsonStringEnumMemberName("jsonpath")]
    JsonPath,

    /// <summary>
    /// 字符串插值 - 模板字符串，替换 {变量引用}，如 "请总结：{nodeA.answer}".
    /// </summary>
    [JsonStringEnumMemberName("interpolation")]
    Interpolation,
}
