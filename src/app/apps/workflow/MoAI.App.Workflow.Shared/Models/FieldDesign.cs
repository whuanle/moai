using MoAI.Workflow.Enums;

namespace MoAI.Workflow.Models;

/// <summary>
/// 字段设计模型，定义节点输入字段的配置方式.
/// </summary>
public class FieldDesign
{
    /// <summary>
    /// 字段名称.
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// 表达式类型，定义如何为字段分配值.
    /// </summary>
    public FieldExpressionType ExpressionType { get; set; }

    /// <summary>
    /// 字段值或表达式.
    /// 根据 ExpressionType 的不同，可以是：
    /// - Fixed: 固定的常数值
    /// - Variable: 变量引用（如 sys.userId, input.name, nodeKey.output）
    /// - JsonPath: JSON 路径表达式（如 nodeA.result[0].name）
    /// - StringInterpolation: 字符串插值模板（如 "Hello {input.name}"）
    /// </summary>
    public string Value { get; set; } = string.Empty;
}
