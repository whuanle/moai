#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

using System.Text.Json.Serialization;

namespace MoAI.Infra.BoCha.Models;

/// <summary>
/// AI Search 响应数据
/// </summary>
public class AiSearchResponse : BoChaCode
{
    /// <summary>
    /// 会话 ID.
    /// </summary>
    [JsonPropertyName("conversation_id")]
    public string ConversationId { get; init; }

    /// <summary>
    /// 数据.
    /// </summary>
    [JsonPropertyName("messages")]
    public List<MessageObject> Messages { get; init; }
}