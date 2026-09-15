using System.Text.Json.Serialization;

namespace MoAI.Database.Enums;

/// <summary>
/// 上架审核状态.
/// </summary>
public enum PublicationState
{
    /// <summary>
    /// 待审核.
    /// </summary>
    [JsonPropertyName("pending")]
    Pending = 0,

    /// <summary>
    /// 已通过，资源 is_public 置为 true.
    /// </summary>
    [JsonPropertyName("approved")]
    Approved = 1,

    /// <summary>
    /// 已驳回.
    /// </summary>
    [JsonPropertyName("rejected")]
    Rejected = 2,
}
