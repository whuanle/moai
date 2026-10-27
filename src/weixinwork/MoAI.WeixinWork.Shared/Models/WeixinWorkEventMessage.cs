using MoAI.Database.Enums;

namespace MoAI.WeixinWork.Models;

/// <summary>
/// 企业微信事件消息，企业微信模块从回调推送解密后按绑定关系封装，转发给渠道对应的 <see cref="Services.IWeixinWorkEventHandler"/>.
/// </summary>
public class WeixinWorkEventMessage
{
    /// <summary>
    /// 企业微信应用记录 id（weixinwork_app.id）.
    /// </summary>
    public Guid WeixinWorkAppId { get; init; }

    /// <summary>
    /// 企业 ID（解密报文 receiveid，已与连接配置比对）.
    /// </summary>
    public string CorpId { get; init; } = default!;

    /// <summary>
    /// 回调报文 AgentID.
    /// </summary>
    public int AgentId { get; init; }

    /// <summary>
    /// 推送 id（MsgId），企业微信侧全局唯一，同一推送只投递一次.
    /// </summary>
    public string MessageId { get; init; } = default!;

    /// <summary>
    /// 发送消息的成员 userid.
    /// </summary>
    public string FromUserId { get; init; } = default!;

    /// <summary>
    /// 群聊 id，群聊消息独有；单聊为空字符串.
    /// </summary>
    public string ChatId { get; init; } = string.Empty;

    /// <summary>
    /// 消息类型：text/image/voice/video/location/file/event 等.
    /// </summary>
    public string MessageType { get; init; } = string.Empty;

    /// <summary>
    /// 文本消息内容，非 text 类型为空字符串.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 解密后的原始报文 XML，由处理器自行扩展解析.
    /// </summary>
    public string Payload { get; init; } = default!;

    /// <summary>
    /// 绑定的渠道类型.
    /// </summary>
    public WeixinWorkChannelType ChannelType { get; init; }

    /// <summary>
    /// 绑定的渠道记录 id 字符串.
    /// </summary>
    public string ChannelId { get; init; } = default!;
}
