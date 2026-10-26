using MoAI.Database.Enums;

namespace MoAI.DingTalk.Models;

/// <summary>
/// 钉钉事件消息，钉钉模块从 Stream 长连接收到推送后按绑定关系封装，转发给渠道对应的 <see cref="Services.IDingTalkEventHandler"/>.
/// </summary>
public class DingTalkEventMessage
{
    /// <summary>
    /// 机器人消息回调订阅 Topic.
    /// </summary>
    public const string RobotMessageTopic = "/v1.0/im/bot/messages/get";

    /// <summary>
    /// 钉钉应用记录 id（dingtalk_app.id）.
    /// </summary>
    public Guid DingTalkAppId { get; init; }

    /// <summary>
    /// 钉钉应用 AppKey.
    /// </summary>
    public string AppKey { get; init; } = default!;

    /// <summary>
    /// 推送 Topic，机器人消息为 <see cref="RobotMessageTopic"/>.
    /// </summary>
    public string Topic { get; init; } = default!;

    /// <summary>
    /// 推送 id，钉钉侧全局唯一，同一推送只投递一次.
    /// </summary>
    public string MessageId { get; init; } = default!;

    /// <summary>
    /// 推送时间（钉钉侧毫秒时间戳字符串）.
    /// </summary>
    public string Time { get; init; } = default!;

    /// <summary>
    /// 推送内容，为 data 节点原始 JSON 字符串，由处理器自行反序列化.
    /// </summary>
    public string Payload { get; init; } = default!;

    /// <summary>
    /// 会话 webhook，机器人消息独有；处理器用它向原会话回复消息（有效期见钉钉文档，即时回复即可）.
    /// </summary>
    public string SessionWebhook { get; init; } = default!;

    /// <summary>
    /// 绑定的渠道类型.
    /// </summary>
    public DingTalkChannelType ChannelType { get; init; }

    /// <summary>
    /// 绑定的渠道记录 id 字符串.
    /// </summary>
    public string ChannelId { get; init; } = default!;
}
