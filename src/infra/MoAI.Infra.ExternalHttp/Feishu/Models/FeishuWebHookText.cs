using System.Text.Json.Serialization;

namespace MoAI.Infra.Feishu.Models;

public class FeishuWebHookText
{
    /// <summary>
    /// 消息内容.
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; init; }
}
