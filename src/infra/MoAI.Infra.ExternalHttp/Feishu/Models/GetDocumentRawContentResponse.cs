using System.Text.Json.Serialization;

namespace MoAI.Infra.Feishu.Models;

/// <summary>
/// 获取文档纯文本内容的响应。
/// </summary>
public class GetDocumentRawContentResponse : FeishuCode
{
    /// <summary>
    /// 响应数据。
    /// </summary>
    [JsonPropertyName("data")]
    public required DocumentRawContentData Data { get; init; }
}