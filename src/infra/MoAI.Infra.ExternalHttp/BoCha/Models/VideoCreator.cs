using System.Text.Json.Serialization;

namespace MoAI.Infra.BoCha.Models;

/// <summary>
/// 视频发布者信息。
/// </summary>
public class VideoCreator
{
    /// <summary>
    /// 发布者名称。
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// 发布者头像链接。
    /// </summary>
    [JsonPropertyName("avatar_url")]
    public string AvatarUrl { get; set; }

    /// <summary>
    /// 发布者粉丝数量。
    /// </summary>
    [JsonPropertyName("followers_count")]
    public int FollowersCount { get; set; }
}
