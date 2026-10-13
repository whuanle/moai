using System.Collections.Generic;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// SMTP 邮件发送插件响应.
/// </summary>
public class SmtpEmailSendResponse
{
    /// <summary>
    /// 本封邮件的 Message-ID.
    /// </summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// 实际发件人地址.
    /// </summary>
    public string From { get; set; } = string.Empty;

    /// <summary>
    /// 收件人（归一化后）.
    /// </summary>
    public IReadOnlyList<string> To { get; set; } = new List<string>();

    /// <summary>
    /// 抄送（归一化后，未填为空列表）.
    /// </summary>
    public IReadOnlyList<string> Cc { get; set; } = new List<string>();

    /// <summary>
    /// 密送（归一化后，未填为空列表）.
    /// </summary>
    public IReadOnlyList<string> Bcc { get; set; } = new List<string>();

    /// <summary>
    /// 邮件主题.
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// 附件数量.
    /// </summary>
    public int AttachmentCount { get; set; }

    /// <summary>
    /// 附件总字节数.
    /// </summary>
    public long AttachmentTotalBytes { get; set; }
}
