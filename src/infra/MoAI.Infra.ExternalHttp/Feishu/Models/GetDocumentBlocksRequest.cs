using Refit;
using System.Text.Json.Serialization;

namespace MoAI.Infra.Feishu.Models;

/// <summary>
/// 获取文档所有块的请求。
/// </summary>
public class GetDocumentBlocksRequest
{
    /// <summary>
    /// 分页大小。
    /// </summary>
    /// <remarks>
    /// 默认值：500，最大值：500。
    /// </remarks>
    [AliasAs("page_size")]
    [JsonPropertyName("page_size")]
    public int? PageSize { get; set; }

    /// <summary>
    /// 分页标记，第一次请求不填，表示从头开始遍历。
    /// </summary>
    [AliasAs("page_token")]
    [JsonPropertyName("page_token")]
    public string? PageToken { get; set; }

    /// <summary>
    /// 查询的文档版本，-1 表示文档最新版本。
    /// </summary>
    /// <remarks>
    /// 默认值：-1，最小值：-1。
    /// </remarks>
    [AliasAs("document_revision_id")]
    [JsonPropertyName("document_revision_id")]
    public int? DocumentRevisionId { get; set; }

    /// <summary>
    /// 用户 ID 类型。
    /// </summary>
    /// <remarks>
    /// 可选值有："open_id", "union_id", "user_id"。默认值："open_id"。
    /// </remarks>
    [AliasAs("user_id_type")]
    [JsonPropertyName("user_id_type")]
    public string? UserIdType { get; set; }
}