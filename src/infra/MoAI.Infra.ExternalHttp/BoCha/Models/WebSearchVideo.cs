using System.Text.Json.Serialization;

namespace MoAI.Infra.BoCha.Models;

/// <summary>
/// 视频搜索结果
/// </summary>
public class WebSearchVideoValue
{
    /// <summary>
    /// 视频类型，例如 "short_video"。
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; }

    /// <summary>
    /// 视频描述。
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; }

    /// <summary>
    /// 视频宽度。
    /// </summary>
    [JsonPropertyName("width")]
    public int Width { get; set; }

    /// <summary>
    /// 视频高度。
    /// </summary>
    [JsonPropertyName("height")]
    public int Height { get; set; }

    /// <summary>
    /// 视频清晰度，例如 "540p"。
    /// </summary>
    [JsonPropertyName("definition")]
    public string Definition { get; set; }

    /// <summary>
    /// 视频时长（秒）。
    /// </summary>
    [JsonPropertyName("duration")]
    public int Duration { get; set; }

    /// <summary>
    /// 视频大小（字节）。
    /// </summary>
    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>
    /// 视频发布者信息。
    /// </summary>
    [JsonPropertyName("creator")]
    public VideoCreator Creator { get; set; }

    /// <summary>
    /// 视频交互数据。
    /// </summary>
    [JsonPropertyName("interactions")]
    public VideoInteractions Interactions { get; set; }

    /// <summary>
    /// 视频发布时间（Unix 时间戳）。
    /// </summary>
    [JsonPropertyName("publish_time")]
    public long PublishTime { get; set; }

    /// <summary>
    /// 视频文章链接。
    /// </summary>
    [JsonPropertyName("article_url")]
    public string ArticleUrl { get; set; }

    /// <summary>
    /// 视频内容链接。
    /// </summary>
    [JsonPropertyName("content_url")]
    public string ContentUrl { get; set; }

    /// <summary>
    /// 视频封面图片列表。
    /// </summary>
    [JsonPropertyName("cover_images")]
    public List<VideoCoverImage> CoverImages { get; set; }
}
