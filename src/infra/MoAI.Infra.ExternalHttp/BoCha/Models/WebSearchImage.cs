#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

using System.Text.Json.Serialization;

namespace MoAI.Infra.BoCha.Models;

/// <summary>
/// Web Search 图片结果
/// </summary>
public class WebSearchImage
{
    /// <summary>
    /// 网页的排序 ID
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// 链接.
    /// </summary>
    [JsonPropertyName("readLink")]
    public string? ReadLink { get; init; }

    /// <summary>
    /// 搜索的网页 URL
    /// </summary>
    [JsonPropertyName("webSearchUrl")]
    public string? WebSearchUrl { get; init; }

    /// <summary>
    /// boolean 是否为家庭友好的页面
    /// </summary>
    [JsonPropertyName("IsFamilyFriendly")]
    public bool? IsFamilyFriendly { get; init; }

    /// <summary>
    /// 网页结果列表
    /// </summary>
    [JsonPropertyName("value")]
    public List<WebSearchImageValue> Value { get; init; }
}