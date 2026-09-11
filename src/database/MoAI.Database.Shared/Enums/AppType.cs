using System.Text.Json.Serialization;

namespace MoAI.Database.Enums;

/// <summary>
/// 应用类型.
/// </summary>
public enum AppType
{
    /// <summary>
    /// Agent 应用，以模型 + 提示词 + 知识库/插件编排的对话式应用.
    /// </summary>
    [JsonPropertyName("agent")]
    Agent = 0,

    /// <summary>
    /// 流程应用，以流程编排驱动的应用.
    /// </summary>
    [JsonPropertyName("workflow")]
    Workflow = 1,
}
