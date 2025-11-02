using System.Text.Json.Serialization;

namespace MoAI.Infra.Feishu.Models;


/// <summary>
/// 分享群名片.
/// </summary>
public class FeishuWebHookShareChat
{
    /// <summary>
    /// 群id.
    /// </summary>
    [JsonPropertyName("share_chat_id")]
    public string ShareChatId { get; init; }
}
