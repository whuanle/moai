#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

using System.Text.Json.Serialization;

namespace MoAI.Infra.BoCha.Models;

/// <summary>
/// Web Search 数据
/// </summary>
public class WebSearchData
{
    /// <summary>
    /// 搜索的类型
    /// </summary>
    [JsonPropertyName("_type")]
    public string Type { get; init; }

    /// <summary>
    /// 查询上下文
    /// </summary>
    [JsonPropertyName("queryContext")]
    public WebSearchQueryContext QueryContext { get; init; }

    /// <summary>
    /// 搜索的网页结果
    /// </summary>
    [JsonPropertyName("webPages")]
    public WebSearchWebPages? WebPages { get; init; }

    /// <summary>
    /// 搜索的图片结果
    /// </summary>
    [JsonPropertyName("images")]
    public WebSearchImage? Images { get; init; }

    /// <summary>
    /// 搜索的视频结果
    /// </summary>
    [JsonPropertyName("videos")]
    public WebSearchVideo? Videos { get; init; }
}