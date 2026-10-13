using System.Collections.Generic;
using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// SMTP 邮件发送插件请求参数.
/// </summary>
public class SmtpEmailSendRequest
{
    /// <summary>
    /// 收件人（多个用逗号或分号分隔）.
    /// </summary>
    [Description("收件人邮箱，多个用逗号或分号分隔，如 a@x.com;b@x.com（必填）")]
    public string To { get; set; } = string.Empty;

    /// <summary>
    /// 抄送（可空）.
    /// </summary>
    [Description("抄送邮箱，多个用逗号或分号分隔（可选）")]
    public string? Cc { get; set; }

    /// <summary>
    /// 密送（可空）.
    /// </summary>
    [Description("密送邮箱，多个用逗号或分号分隔（可选）")]
    public string? Bcc { get; set; }

    /// <summary>
    /// 邮件主题.
    /// </summary>
    [Description("邮件主题（必填，最长 500 字符）")]
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// 邮件正文.
    /// </summary>
    [Description("邮件正文（必填，纯文本或 HTML，最长 100000 字符）")]
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// 正文是否为 HTML.
    /// </summary>
    [Description("正文是否为 HTML（默认 false=纯文本）")]
    public bool IsHtml { get; set; }

    /// <summary>
    /// 回复地址（可空）.
    /// </summary>
    [Description("回复地址 Reply-To（可选），收件人点「回复」时发到此地址")]
    public string? ReplyTo { get; set; }

    /// <summary>
    /// 附件列表.
    /// </summary>
    [Description("附件列表（可选），每项 {\"FileName\":\"报表.xlsx\",\"ContentBase64\":\"...\"}，最多 10 个，内容为文件二进制的 Base64")]
    public List<SmtpEmailAttachmentItem>? Attachments { get; set; }
}

/// <summary>
/// SMTP 附件条目.
/// </summary>
public class SmtpEmailAttachmentItem
{
    /// <summary>
    /// 附件文件名.
    /// </summary>
    [Description("附件文件名（必填），如 月度报表.xlsx")]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 文件内容的 Base64.
    /// </summary>
    [Description("文件二进制内容的 Base64 编码（必填）")]
    public string ContentBase64 { get; set; } = string.Empty;
}
