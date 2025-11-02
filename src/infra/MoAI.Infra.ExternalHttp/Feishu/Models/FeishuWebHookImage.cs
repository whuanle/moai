using System.Text.Json.Serialization;

namespace MoAI.Infra.Feishu.Models;

public class FeishuWebHookImage
{
    /// <summary>
    /// 消息内容.
    /// </summary>
    [JsonPropertyName("image_key")]
    public string ImageKey { get; init; }
}
