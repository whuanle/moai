using System.Text.Json.Serialization;

namespace MoAI.Message.Models;

/// <summary>
/// 站内信消息类型.
/// </summary>
public enum MessageType
{
    /// <summary>
    /// 系统通知（平台/模块下发，无具体发送人）.
    /// </summary>
    [JsonPropertyName("system")]
    System = 1,

    /// <summary>
    /// 团队消息（团队事件触达成员）.
    /// </summary>
    [JsonPropertyName("team")]
    Team = 2
}
