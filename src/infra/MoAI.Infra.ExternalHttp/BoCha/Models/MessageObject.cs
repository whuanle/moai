#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

using System.Text.Json.Serialization;

namespace MoAI.Infra.BoCha.Models;

/// <summary>
/// 表示一条消息的对象。
/// </summary>
public class MessageObject
{
    /// <summary>
    /// 发送这条消息的实体。
    /// 取值：
    /// - user：代表该条消息内容是用户发送的。
    /// - assistant：代表该条消息内容是 博查 发送的。
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; init; }

    /// <summary>
    /// 当 role=assistant 时，用于标识消息类型。
    /// 取值：
    /// - source: 参考源，此时 content_type 会有 webpage、video、image 三种。
    /// - answer：最终返回给用户的消息内容。
    /// - follow_up：推荐问题。
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>
    /// 消息内容。
    /// - 当 type=answer 时，content_type=text，消息内容是 Markdown 格式。
    /// - 如果 content_type != text，返回的是 JSON encode 后的文本。
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; init; }

    /// <summary>
    /// 消息内容的类型。
    /// - text: 文本类型。
    /// - type=source 时，可能的取值包括：
    ///   - webpage：网页 Object。
    ///   - image: 图片 Object。
    ///   - video：视频 Object。
    ///   - 其他多模态搜索到的参考源类型。
    /// </summary>
    [JsonPropertyName("content_type")]
    public string ContentType { get; init; }
}