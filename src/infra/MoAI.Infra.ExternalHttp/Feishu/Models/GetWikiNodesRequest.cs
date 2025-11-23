using Refit;
using System.Text.Json.Serialization;

namespace MoAI.Infra.Feishu.Models;

/// <summary>
/// 获取知识空间子节点列表的请求。
/// </summary>
public class GetWikiNodesRequest
{
    /// <summary>
    /// 分页大小。
    /// </summary>
    /// <remarks>
    /// 最大值：50。
    /// </remarks>
    /// <example>10</example>
    [AliasAs("page_size")]
    [JsonPropertyName("page_size")]
    public int? PageSize { get; init; }

    /// <summary>
    /// 分页标记，第一次请求不填，表示从头开始遍历。
    /// </summary>
    /// <example>6946843325487456878</example>
    [AliasAs("page_token")]
    [JsonPropertyName("page_token")]
    public string? PageToken { get; init; }

    /// <summary>
    /// 父节点 token。
    /// </summary>
    /// <example>wikcnKQ1k3p******8Vabce</example>
    [AliasAs("parent_node_token")]
    [JsonPropertyName("parent_node_token")]
    public string? ParentNodeToken { get; init; }
}