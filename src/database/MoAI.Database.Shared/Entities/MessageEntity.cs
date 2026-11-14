using System;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 站内信：每收件人一行，已读态/软删独立，列表按接收人隔离.
/// </summary>
public partial class MessageEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 接收人用户 id.
    /// </summary>
    public long RecipientUserId { get; set; }

    /// <summary>
    /// 标题.
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// 正文.
    /// </summary>
    public string Content { get; set; } = default!;

    /// <summary>
    /// 消息类型.
    /// </summary>
    public int MessageType { get; set; }

    /// <summary>
    /// 来源模块标识，如 publication / team，空为平台.
    /// </summary>
    public string Source { get; set; } = default!;

    /// <summary>
    /// 发送人用户 id，空为系统发送.
    /// </summary>
    public long? SenderUserId { get; set; }

    /// <summary>
    /// 扩展参数（可选 JSON 文本，供前端跳转定位）.
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

    /// <summary>
    /// 创建人.
    /// </summary>
    public long CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新人.
    /// </summary>
    public long UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
