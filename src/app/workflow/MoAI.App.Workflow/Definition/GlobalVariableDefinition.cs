using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
namespace MoAI.App.Workflow.Definition;

/// <summary>
/// 全局变量定义 - 流程级变量，所有节点可通过 system.变量名 引用.
/// 启动时可传入实际值覆盖默认值（未传的字段使用 <see cref="DefaultValue"/>）.
/// </summary>
public class GlobalVariableDefinition
{
    /// <summary>
    /// 变量名（英文字母/数字/下划线），通过 system.变量名 引用.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 期望字段类型（设计器元数据）.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FieldType? FieldType { get; set; }

    /// <summary>
    /// 默认值（JSON 字面量字符串：数字/布尔按字面量解析，其余按字符串；空为 null）.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DefaultValue { get; set; }

    /// <summary>
    /// 变量描述.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    /// <summary>
    /// 解析默认值为 JSON 节点：数字/布尔按 JSON 字面量解析，解析失败按字符串处理；空值返回 null.
    /// </summary>
    public JsonNode? ResolveDefaultValue()
    {
        if (string.IsNullOrWhiteSpace(DefaultValue))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(DefaultValue);
        }
#pragma warning disable CA1031 // 默认值解析失败按纯文本处理，不阻断启动
        catch (Exception)
        {
            return JsonValue.Create(DefaultValue);
        }
#pragma warning restore CA1031
    }
}
