using System.Text.Json.Serialization;

namespace MoAI.App.Workflow.Definition;

/// <summary>
/// 字段绑定模型 - 描述节点输入字段如何取值（前端设计器中"上游输出 → 下游输入"的连线映射）.
/// </summary>
public class FieldBinding
{
    /// <summary>
    /// 表达式类型，决定 <see cref="Value"/> 如何被解析.
    /// </summary>
    public ExpressionType ExpressionType { get; set; } = ExpressionType.Fixed;

    /// <summary>
    /// 表达式内容：
    /// Fixed - 常量值；Variable - 变量引用（如 search.documents）；JsonPath - 查询表达式；Interpolation - 模板字符串.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// 是否必需（默认 true）。分支汇合节点可引用未执行分支的输出并设为 false，解析失败时该输入为 null.
    /// </summary>
    public bool Required { get; set; } = true;

    /// <summary>
    /// 期望字段类型（设计器元数据，引擎运行时不校验），null 表示未指定（运行时）.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FieldType? FieldType { get; set; }

    /// <summary>
    /// 字段描述.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }
}
