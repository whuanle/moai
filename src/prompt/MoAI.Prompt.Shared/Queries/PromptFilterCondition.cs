using System.Text.Json.Serialization;

namespace MoAIPrompt.Queries;

public enum PromptFilterCondition
{
    /// <summary>
    /// 无限制.
    /// </summary>
    [JsonPropertyName("none")]
    None,

    /// <summary>
    /// 只查自己的.
    /// </summary>
    [JsonPropertyName("own")]
    Own,

    /// <summary>
    /// 我共享的.
    /// </summary>
    [JsonPropertyName("ownpublic")]
    OwnPublic,

    /// <summary>
    /// 私密的.
    /// </summary>
    [JsonPropertyName("ownpublic")]
    OwnPrivate,

    /// <summary>
    /// 他人共享的.
    /// </summary>
    [JsonPropertyName("othershare")]
    OtherShare
}