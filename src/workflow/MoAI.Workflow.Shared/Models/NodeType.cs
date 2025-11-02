using System.Text.Json.Serialization;

namespace MoAI.Workflow.Models;

/// <summary>
/// 节点类型.
/// </summary>
public enum NodeType
{
    [JsonPropertyName("start")]
    Start,

    [JsonPropertyName("end")]
    End,

    [JsonPropertyName("plugin")]
    Plugin,

    [JsonPropertyName("wiki")]
    Wiki,

    [JsonPropertyName("ai_question")]
    AiQuestion,

    [JsonPropertyName("http")]
    Http,

    [JsonPropertyName("javascript")]
    JavaScript,

    [JsonPropertyName("condition")]
    Condition,

    [JsonPropertyName("fork")]
    Fork,

    [JsonPropertyName("join")]
    Join
}
