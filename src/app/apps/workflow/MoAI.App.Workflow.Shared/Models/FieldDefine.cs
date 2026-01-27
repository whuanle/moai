using MoAI.Workflow.Enums;

namespace MoAI.Workflow.Models;

/// <summary>
/// 字段定义模型，描述节点输入或输出字段的结构.
/// </summary>
public class FieldDefine
{
    /// <summary>
    /// 字段名称.
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// 字段数据类型.
    /// </summary>
    public FieldType FieldType { get; set; }

    /// <summary>
    /// 是否为必需字段.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// 字段描述信息.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
