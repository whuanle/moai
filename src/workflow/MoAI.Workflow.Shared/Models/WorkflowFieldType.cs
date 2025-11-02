using System.Text.Json.Serialization;

namespace MoAI.Workflow.Models;

public enum WorkflowFieldType
{
    [JsonPropertyName("string")]
    String,

    [JsonPropertyName("number")]
    Number,

    [JsonPropertyName("boolean")]
    Boolean,

    [JsonPropertyName("integer")]
    Integer,

    [JsonPropertyName("object")]
    Object,

    [JsonPropertyName("array")]
    Array,

    [JsonPropertyName("map")]
    Map
}
