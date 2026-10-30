namespace MoAI.Settings.Models;

/// <summary>
/// 待发送的通知邮件.
/// </summary>
public class EmailMessage
{
    /// <summary>
    /// 收件人邮箱地址列表.
    /// </summary>
    public IReadOnlyList<string> To { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 邮件主题.
    /// </summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>
    /// 邮件正文.
    /// </summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>
    /// 正文是否为 HTML（false 为纯文本）.
    /// </summary>
    public bool IsHtml { get; init; }
}

/// <summary>
/// 邮件发送结果.
/// </summary>
public class EmailSendResult
{
    /// <summary>
    /// 邮件 Message-ID.
    /// </summary>
    public string MessageId { get; init; } = string.Empty;

    /// <summary>
    /// 实际使用的发件人地址.
    /// </summary>
    public string From { get; init; } = string.Empty;
}
