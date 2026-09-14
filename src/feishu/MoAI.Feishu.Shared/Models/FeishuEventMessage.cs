using MoAI.Database.Enums;

namespace MoAI.Feishu.Models;

/// <summary>
/// 飞书事件消息，飞书模块从长连接收到事件后按绑定关系封装，转发给渠道对应的 <see cref="Services.IFeishuEventHandler"/>.
/// </summary>
public class FeishuEventMessage
{
    /// <summary>
    /// 飞书应用记录 id（feishu_app.id）.
    /// </summary>
    public Guid FeishuAppId { get; init; }

    /// <summary>
    /// 飞书开放平台 AppID.
    /// </summary>
    public string AppId { get; init; } = default!;

    /// <summary>
    /// 飞书租户 key.
    /// </summary>
    public string TenantKey { get; init; } = default!;

    /// <summary>
    /// 事件 id，飞书侧全局唯一，同一事件只投递一次.
    /// </summary>
    public string EventId { get; init; } = default!;

    /// <summary>
    /// 事件类型，如 im.message.receive_v1.
    /// </summary>
    public string EventType { get; init; } = default!;

    /// <summary>
    /// 事件发生时间（飞书侧毫秒时间戳字符串）.
    /// </summary>
    public string CreateTime { get; init; } = default!;

    /// <summary>
    /// 事件内容，为 envelope 的 event 节点原始 JSON，由处理器自行反序列化.
    /// </summary>
    public string Payload { get; init; } = default!;

    /// <summary>
    /// 绑定的渠道类型.
    /// </summary>
    public FeishuChannelType ChannelType { get; init; }

    /// <summary>
    /// 绑定的渠道记录 id 字符串.
    /// </summary>
    public string ChannelId { get; init; } = default!;
}
