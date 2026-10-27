using System.Text.Json.Serialization;

namespace MoAI.Database.Enums;

/// <summary>
/// 企业微信渠道类型.
/// </summary>
public enum WeixinWorkChannelType
{
    /// <summary>
    /// 团队应用，接收企业微信自建应用回调消息（单聊/群聊），运行应用 Agent 后回复；独占型渠道.
    /// </summary>
    [JsonPropertyName("app")]
    App = 0,
}
