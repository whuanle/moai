#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

using System.Text.Json.Serialization;

namespace MoAI.Infra.BoCha.Models;

/// <summary>
/// Web Search 单条结果
/// </summary>
public class WebSearchResult
{
    /// <summary>
    /// 网页的排序 ID
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; }

    /// <summary>
    /// 网页的标题
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; }

    /// <summary>
    /// 网页的 URL
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; init; }

    /// <summary>
    /// 网页的展示 URL
    /// </summary>
    [JsonPropertyName("displayUrl")]
    public string DisplayUrl { get; init; }

    /// <summary>
    /// 网页内容的简短描述
    /// </summary>
    [JsonPropertyName("snippet")]
    public string Snippet { get; init; }

    /// <summary>
    /// 网页内容的文本摘要
    /// </summary>
    [JsonPropertyName("summary")]
    public string Summary { get; init; }

    /// <summary>
    /// 网页的网站名称
    /// </summary>
    [JsonPropertyName("siteName")]
    public string SiteName { get; init; }

    /// <summary>
    /// 网页的网站图标
    /// </summary>
    [JsonPropertyName("siteIcon")]
    public string SiteIcon { get; init; }

    /// <summary>
    /// 网页的发布时间
    /// </summary>
    [JsonPropertyName("datePublished")]
    public string DatePublished { get; init; }

    /// <summary>
    /// 网页的缓存页面 URL
    /// </summary>
    [JsonPropertyName("cachedPageUrl")]
    public string? CachedPageUrl { get; init; }

    /// <summary>
    /// 网页的语言
    /// </summary>
    [JsonPropertyName("language")]
    public string? Language { get; init; }

    /// <summary>
    /// 是否为家庭友好的页面
    /// </summary>
    [JsonPropertyName("isFamilyFriendly")]
    public bool? IsFamilyFriendly { get; init; }

    /// <summary>
    /// 是否为导航性页面
    /// </summary>
    [JsonPropertyName("isNavigational")]
    public bool? IsNavigational { get; init; }
}
