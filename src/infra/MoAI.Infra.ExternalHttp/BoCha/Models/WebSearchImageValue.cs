#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

using System.Text.Json.Serialization;

namespace MoAI.Infra.BoCha.Models;

/// <summary>
/// Web Search 图片结果
/// </summary>
public class WebSearchImageValue
{
    /// <summary>
    /// 搜索的图片 URL
    /// </summary>
    [JsonPropertyName("webSearchUrl")]
    public string? WebSearchUrl { get; init; }

    /// <summary>
    /// 图片名称
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// 图片缩略图 URL
    /// </summary>
    [JsonPropertyName("thumbnailUrl")]
    public string? ThumbnailUrl { get; init; }

    /// <summary>
    /// 图片的发布时间
    /// </summary>
    [JsonPropertyName("datePublished")]
    public string? DatePublished { get; init; }

    /// <summary>
    /// 图片内容 URL
    /// </summary>
    [JsonPropertyName("contentUrl")]
    public string ContentUrl { get; init; }

    /// <summary>
    /// 图片所在页面 URL
    /// </summary>
    [JsonPropertyName("hostPageUrl")]
    public string? HostPageUrl { get; init; }

    /// <summary>
    /// 图片内容大小
    /// </summary>
    [JsonPropertyName("contentSize")]
    public int? ContentSize { get; init; }

    /// <summary>
    /// 图片编码格式
    /// </summary>
    [JsonPropertyName("encodingFormat")]
    public string EncodingFormat { get; init; }

    /// <summary>
    /// 图片所在页面的展示 URL
    /// </summary>
    [JsonPropertyName("hostPageDisplayUrl")]
    public string? HostPageDisplayUrl { get; init; }

    /// <summary>
    /// 图片宽度
    /// </summary>
    [JsonPropertyName("width")]
    public int? Width { get; init; }

    /// <summary>
    /// 图片高度
    /// </summary>
    [JsonPropertyName("height")]
    public int? Height { get; init; }
}