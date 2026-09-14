using System.Text.Json.Serialization;

namespace MoAI.Database.Enums;

/// <summary>
/// 飞书应用绑定的渠道类型；当前仅应用渠道（群聊/私聊消息回复），后续渠道（如知识库）扩展枚举值.
/// </summary>
public enum FeishuChannelType
{
    /// <summary>
    /// 团队应用，接收飞书群聊/私聊消息事件，运行应用 Agent 后回复.
    /// </summary>
    [JsonPropertyName("app")]
    App = 0,
}
