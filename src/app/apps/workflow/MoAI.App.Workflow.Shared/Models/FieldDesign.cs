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
    /// 字段描述.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 字段数据类型.
    /// </summary>
    public FieldType FieldType { get; set; }

    /// <summary>
    /// 表达式类型，定义如何为字段分配值.
    /// </summary>
    public FieldExpressionType ExpressionType { get; set; }

    /// <summary>
    /// 字段值或表达式.
    /// 根据 ExpressionType 的不同，可以是：
    /// - Run: 运行时传入的值（不允许手动设置，此选项只能在开始节点使用）
    /// - Fixed: 固定的常数值
    /// - Variable: 变量引用（如 sys.userId, input.name, nodeKey.output）
    /// - Jsonpath: JSON 路径表达式（如 nodeA.result[0].name）
    /// - Interpolation: 字符串插值模板（如 "Hello {input.name}"）
    /// </summary>
    public string Value { get; set; } = string.Empty;
}
