using System.Text.Json.Serialization;

namespace MoAI.Database.Enums;

/// <summary>
/// 钉钉渠道类型.
/// </summary>
public enum DingTalkChannelType
{
    /// <summary>
    /// 团队应用，接收钉钉机器人消息（群聊/私聊），运行应用 Agent 后回复；独占型渠道.
    /// </summary>
    [JsonPropertyName("app")]
    App = 0,
}
