using System.Text.Json.Serialization;

namespace MoAI.AIChannel.Models;

/// <summary>
/// AI 模型类型.
/// </summary>
public enum AIModelKind
{
    /// <summary>
    /// 对话/语言模型.
    /// </summary>
    [JsonPropertyName("conversation")]
    Conversation = 0,

    /// <summary>
    /// 嵌入模型.
    /// </summary>
    [JsonPropertyName("embedding")]
    Embedding = 1,

    /// <summary>
    /// 生成图模型.
    /// </summary>
    [JsonPropertyName("image-generation")]
    ImageGeneration = 2,

    /// <summary>
    /// 转写模型.
    /// </summary>
    [JsonPropertyName("transcription")]
    Transcription = 3,

    /// <summary>
    /// 生成视频模型.
    /// </summary>
    [JsonPropertyName("video-generation")]
    VideoGeneration = 4,
}
