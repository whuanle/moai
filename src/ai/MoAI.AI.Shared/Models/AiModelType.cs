using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace MoAI.AI.Models;

/// <summary>
/// AI 模型大分类.
/// </summary>
public enum AiModelType
{
    /// <summary>
    /// chat.
    /// </summary>
    [JsonPropertyName("chat")]
    [EnumMember(Value = "chat")]
    Chat,

    /// <summary>
    /// embedding.
    /// </summary>
    [JsonPropertyName("embedding")]
    [EnumMember(Value = "embedding")]
    Embedding,

    ///// <summary>
    ///// image.
    ///// </summary>
    //[JsonPropertyName("image")]
    //[EnumMember(Value = "image")]
    //Image,

    ///// <summary>
    ///// tts.
    ///// </summary>
    //[JsonPropertyName("tts")]
    //[EnumMember(Value = "tts")]
    //TTS,

    ///// <summary>
    ///// stts.
    ///// </summary>
    //[JsonPropertyName("stts")]
    //[EnumMember(Value = "stts")]
    //STTS,

    ///// <summary>
    ///// realtime.
    ///// </summary>
    //[JsonPropertyName("realtime")]
    //[EnumMember(Value = "realtime")]
    //Realtime,

    ///// <summary>
    ///// text2video.
    ///// </summary>
    //[JsonPropertyName("text2video")]
    //[EnumMember(Value = "text2video")]
    //Text2video,

    ///// <summary>
    ///// text2music.
    ///// </summary>
    //[JsonPropertyName("text2music")]
    //[EnumMember(Value = "text2music")]
    //Text2music,
}
