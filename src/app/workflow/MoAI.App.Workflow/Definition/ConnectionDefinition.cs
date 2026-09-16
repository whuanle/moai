using System.Text.Json.Serialization;

namespace MoAI.App.Workflow.Definition;

/// <summary>
/// 连接定义模型 - 前端设计器中两个节点之间的连线.
/// 既承载流程流转（控制流），也可承载分支条件（条件节点的出边）.
/// 节点之间的数据传输通过 <see cref="NodeDefinition.Inputs"/> 中的变量绑定完成.
/// </summary>
public class ConnectionDefinition
{
    /// <summary>
    /// 连接唯一标识符.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 源节点 Key.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// 目标节点 Key.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// 分支条件（仅条件节点的出边使用）："true" 或 "false"，表示条件结果为真/假时走这条边.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Condition { get; set; }

    /// <summary>
    /// 连接标签（前端展示用，如"满足"/"不满足"）.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Label { get; set; }
}
