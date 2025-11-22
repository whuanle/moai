using System.Text.Json.Serialization;

namespace MoAI.Infra.BoCha.Models;

/// <summary>
/// Semantic Rerank 请求参数
/// </summary>
public class SemanticRerankRequest
{
    /// <summary>
    /// 排序使用的模型版本。
    /// 当前版本模型：
    /// - bocha-semantic-reranker-cn
    /// - bocha-semantic-reranker-en
    /// - gte-rerank
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    /// <summary>
    /// 用户的搜索词。
    /// </summary>
    [JsonPropertyName("query")]
    public string Query { get; init; } = string.Empty;

    /// <summary>
    /// 需要排序的文档数组，最多50个文档。
    /// </summary>
    [JsonPropertyName("documents")]
    public List<string> Documents { get; init; } = new();

    /// <summary>
    /// 排序返回的Top文档数量。默认与documents数量相同。
    /// </summary>
    [JsonPropertyName("top_n")]
    public int? TopN { get; init; }

    /// <summary>
    /// 排序结果列表是否返回每一条document原文。默认：false。
    /// </summary>
    [JsonPropertyName("return_documents")]
    public bool? ReturnDocuments { get; init; }
}