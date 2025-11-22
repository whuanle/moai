using System.Text.Json.Serialization;

namespace MoAI.Infra.BoCha.Models;

/// <summary>
/// 视频封面图片。
/// </summary>
public class VideoCoverImage
{
    /// <summary>
    /// 图片高度。
    /// </summary>
    [JsonPropertyName("height")]
    public int Height { get; set; }

    /// <summary>
    /// 图片宽度。
    /// </summary>
    [JsonPropertyName("width")]
    public int Width { get; set; }

    /// <summary>
    /// 图片链接。
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; }
}