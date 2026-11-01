namespace MoAI.Message.Models;

/// <summary>
/// 站内信发送内容（一次发送对全部收件人相同）.
/// </summary>
public class MessageSendItem
{
    /// <summary>
    /// 标题.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 正文（纯文本）.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 消息类型.
    /// </summary>
    public MessageType MessageType { get; init; } = MessageType.System;

    /// <summary>
    /// 来源模块标识，如 publication / team，空为平台.
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// 发送人用户 id，空为系统发送.
    /// </summary>
    public long? SenderUserId { get; init; }

    /// <summary>
    /// 扩展参数（可选 JSON 文本，供前端跳转定位）.
    /// </summary>
    public string? Params { get; init; }
}
