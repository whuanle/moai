using System.Text.Json.Serialization;

namespace MoAI.Database.Enums;

/// <summary>
/// 上架审核的资源类型.
/// </summary>
public enum PublicationResourceType
{
    /// <summary>
    /// 应用，公开到平台（应用广场）.
    /// </summary>
    [JsonPropertyName("app")]
    App = 0,

    /// <summary>
    /// 提示词.
    /// </summary>
    [JsonPropertyName("prompt")]
    Prompt = 1,
}
