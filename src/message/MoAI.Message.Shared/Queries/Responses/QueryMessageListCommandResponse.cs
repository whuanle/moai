using MoAI.Infra.Models;
using MoAI.Message.Models;

namespace MoAI.Message.Queries.Responses;

/// <summary>
/// 站内信条目：审计列语义为 创建人=发送人（系统消息为空）、创建时间=发送时间.
/// </summary>
public class MessageItem : AuditsInfo
{
    /// <summary>
    /// 站内信 id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 标题.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 正文（纯文本）.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 消息类型.
    /// </summary>
    public MessageType MessageType { get; set; }

    /// <summary>
    /// 来源模块标识.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// 发送人用户 id，空为系统发送.
    /// </summary>
    public long? SenderUserId { get; set; }

    /// <summary>
    /// 扩展参数（可选 JSON 文本）.
    /// </summary>
    public string? Params { get; set; }

    /// <summary>
    /// 是否已读.
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// 已读时间.
    /// </summary>
    public DateTimeOffset? ReadTime { get; set; }
}

/// <summary>
/// 站内信分页结果.
/// </summary>
public class QueryMessageListCommandResponse
{
    /// <summary>
    /// 站内信条目.
    /// </summary>
    public IReadOnlyList<MessageItem> Items { get; set; } = new List<MessageItem>();

    /// <summary>
    /// 总数量.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// 页码.
    /// </summary>
    public int PageNo { get; set; }

    /// <summary>
    /// 每页数量.
    /// </summary>
    public int PageSize { get; set; }
}
