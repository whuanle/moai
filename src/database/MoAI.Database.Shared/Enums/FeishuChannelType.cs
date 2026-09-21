using System.Text.Json.Serialization;

namespace MoAI.Database.Enums;

/// <summary>
/// 飞书应用绑定的渠道类型；渠道分为独占型（同一飞书应用只能绑一个）与订阅型（同一飞书应用可绑多个）.
/// </summary>
public enum FeishuChannelType
{
    /// <summary>
    /// 团队应用，接收飞书群聊/私聊消息事件，运行应用 Agent 后回复；独占型渠道.
    /// </summary>
    [JsonPropertyName("app")]
    App = 0,

    /// <summary>
    /// 知识库外部源，接收飞书云文档事件后重新拉取文档并按外部源工作流处理；订阅型渠道，可与其它渠道共享同一飞书应用.
    /// </summary>
    [JsonPropertyName("wikiSource")]
    WikiSource = 1,
}
