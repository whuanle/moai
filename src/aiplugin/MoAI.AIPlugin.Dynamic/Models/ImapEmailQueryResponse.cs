using System.Collections.Generic;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// IMAP 邮箱读取插件响应（按 Mode 只填充对应字段，其余为空）.
/// </summary>
public class ImapEmailQueryResponse
{
    /// <summary>
    /// 结果类型：recent / search / detail / mark_read / folders.
    /// </summary>
    public string ResultType { get; set; } = string.Empty;

    /// <summary>
    /// 本次操作的文件夹名.
    /// </summary>
    public string Folder { get; set; } = string.Empty;

    /// <summary>
    /// 文件夹内邮件总数（recent/search 模式返回）.
    /// </summary>
    public int? TotalCount { get; set; }

    /// <summary>
    /// recent/search 模式：邮件摘要列表（新→旧，不含正文）.
    /// </summary>
    public IReadOnlyList<ImapEmailMessageSummary> Messages { get; set; } = new List<ImapEmailMessageSummary>();

    /// <summary>
    /// detail 模式：单封邮件完整内容.
    /// </summary>
    public ImapEmailMessageDetail? Message { get; set; }

    /// <summary>
    /// folders 模式：文件夹列表.
    /// </summary>
    public IReadOnlyList<ImapFolderInfo> Folders { get; set; } = new List<ImapFolderInfo>();

    /// <summary>
    /// mark_read 模式：被标记的邮件 UID.
    /// </summary>
    public long? UpdatedUid { get; set; }

    /// <summary>
    /// mark_read 模式：标记后的状态（true=未读，false=已读）.
    /// </summary>
    public bool? Unread { get; set; }

    /// <summary>
    /// 列表是否因超过条数上限被截断.
    /// </summary>
    public bool Truncated { get; set; }
}

/// <summary>
/// IMAP 邮件摘要条目（列表模式）.
/// </summary>
public class ImapEmailMessageSummary
{
    /// <summary>
    /// 邮件 UID（文件夹内唯一，用于 detail/mark_read）.
    /// </summary>
    public long Uid { get; set; }

    /// <summary>
    /// 发件人显示名（可空）.
    /// </summary>
    public string FromName { get; set; } = string.Empty;

    /// <summary>
    /// 发件人地址.
    /// </summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>
    /// 邮件主题.
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// 发信时间（ISO 8601，可空）.
    /// </summary>
    public string DateIso { get; set; } = string.Empty;

    /// <summary>
    /// 是否已读.
    /// </summary>
    public bool Seen { get; set; }

    /// <summary>
    /// 邮件大小（字节）.
    /// </summary>
    public long SizeBytes { get; set; }
}

/// <summary>
/// IMAP 单封邮件完整内容（detail 模式）.
/// </summary>
public class ImapEmailMessageDetail
{
    /// <summary>
    /// 邮件 UID.
    /// </summary>
    public long Uid { get; set; }

    /// <summary>
    /// 邮件 Message-ID.
    /// </summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// 发件人显示名（可空）.
    /// </summary>
    public string FromName { get; set; } = string.Empty;

    /// <summary>
    /// 发件人地址.
    /// </summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>
    /// 收件人列表.
    /// </summary>
    public IReadOnlyList<string> To { get; set; } = new List<string>();

    /// <summary>
    /// 抄送列表.
    /// </summary>
    public IReadOnlyList<string> Cc { get; set; } = new List<string>();

    /// <summary>
    /// 邮件主题.
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// 发信时间（ISO 8601，可空）.
    /// </summary>
    public string DateIso { get; set; } = string.Empty;

    /// <summary>
    /// 是否已读.
    /// </summary>
    public bool Seen { get; set; }

    /// <summary>
    /// 可读正文（纯文本优先，HTML 会剥标签；按 MaxBodyChars 截断）.
    /// </summary>
    public string BodyText { get; set; } = string.Empty;

    /// <summary>
    /// 附件列表（只含元信息，不回传附件内容）.
    /// </summary>
    public IReadOnlyList<ImapEmailAttachment> Attachments { get; set; } = new List<ImapEmailAttachment>();
}

/// <summary>
/// IMAP 附件元信息.
/// </summary>
public class ImapEmailAttachment
{
    /// <summary>
    /// 文件名.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// MIME 类型.
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// 文件大小（字节）.
    /// </summary>
    public long SizeBytes { get; set; }
}

/// <summary>
/// IMAP 文件夹信息（folders 模式）.
/// </summary>
public class ImapFolderInfo
{
    /// <summary>
    /// 显示名（最后一段）.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 完整路径（可作为请求 Folder 参数）.
    /// </summary>
    public string FullName { get; set; } = string.Empty;
}
