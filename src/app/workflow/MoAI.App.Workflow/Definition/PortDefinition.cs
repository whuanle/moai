using System.Text.Json.Serialization;

namespace MoAI.App.Workflow.Definition;

/// <summary>
/// 端口（字段）定义模型 - 描述节点输入或输出字段的结构元数据.
/// 输出端口定义节点会产生哪些数据，供前端在设计器中展示可引用的变量.
/// </summary>
public class PortDefinition
{
    /// <summary>
    /// 字段名称，即节点输出 JSON 对象中的键.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 字段数据类型.
    /// </summary>
    public FieldType FieldType { get; set; } = FieldType.String;

    /// <summary>
    /// 是否为必需字段.
    /// 开始节点使用 <see cref="ExpressionType.Run"/> 定义工作流启动参数时，必需字段会在启动时校验.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// 字段描述.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }
}
