using System.Text.Json.Serialization;

namespace MoAI.Infra.Feishu.Models;

/// <summary>
/// 获取文档所有块的响应。
/// </summary>
public class GetDocumentBlocksResponse : FeishuCode
{
    /// <summary>
    /// 响应数据。
    /// </summary>
    [JsonPropertyName("data")]
    public DocumentBlocksData Data { get; set; }
}

/// <summary>
/// 文档块数据。
/// </summary>
public class DocumentBlocksData
{
    /// <summary>
    /// 文档的 Block 信息。
    /// </summary>
    [JsonPropertyName("items")]
    public List<Block> Items { get; set; } = [];

    /// <summary>
    /// 分页标记。
    /// </summary>
    [JsonPropertyName("page_token")]
    public string? PageToken { get; set; }

    /// <summary>
    /// 是否还有更多项。
    /// </summary>
    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }
}