using System.Text.Json.Serialization;

namespace MoAI.Infra.Feishu.Models;

/// <summary>
/// 文档纯文本内容数据。
/// </summary>
public class DocumentRawContentData
{
    /// <summary>
    /// 文档纯文本内容。
    /// </summary>
    [JsonPropertyName("content")]
    public required string Content { get; init; }
}