using System.Text.Json.Serialization;

namespace MoAI.Infra.BoCha.Models;

/// <summary>
/// 视频交互数据。
/// </summary>
public class VideoInteractions
{
    /// <summary>
    /// 阅读次数。
    /// </summary>
    [JsonPropertyName("read_count")]
    public int ReadCount { get; set; }

    /// <summary>
    /// 点赞次数。
    /// </summary>
    [JsonPropertyName("digg_count")]
    public int DiggCount { get; set; }

    /// <summary>
    /// 分享次数。
    /// </summary>
    [JsonPropertyName("share_count")]
    public int ShareCount { get; set; }

    /// <summary>
    /// 评论次数。
    /// </summary>
    [JsonPropertyName("comment_count")]
    public int CommentCount { get; set; }
}
