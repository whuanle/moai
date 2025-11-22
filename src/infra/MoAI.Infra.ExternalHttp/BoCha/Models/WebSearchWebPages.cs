#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

using System.Text.Json.Serialization;

namespace MoAI.Infra.BoCha.Models;

/// <summary>
/// Web Search Web Pages
/// </summary>
public class WebSearchWebPages
{
    /// <summary>
    /// 搜索的网页 URL
    /// </summary>
    [JsonPropertyName("webSearchUrl")]
    public string WebSearchUrl { get; init; }

    /// <summary>
    /// 搜索匹配的网页总数
    /// </summary>
    [JsonPropertyName("totalEstimatedMatches")]
    public int TotalEstimatedMatches { get; init; }

    /// <summary>
    /// 网页结果列表
    /// </summary>
    [JsonPropertyName("value")]
    public List<WebSearchResult> Value { get; init; }

    /// <summary>
    /// 结果中是否有被安全过滤
    /// </summary>
    [JsonPropertyName("someResultsRemoved")]
    public bool SomeResultsRemoved { get; init; }
}