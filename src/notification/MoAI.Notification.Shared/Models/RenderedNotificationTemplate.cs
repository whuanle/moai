using MoAI.Database.Enums;

namespace MoAI.Notification.Models;

/// <summary>
/// 渲染后的通知模板：变量已插值的最终标题/正文.
/// </summary>
public class RenderedNotificationTemplate
{
    /// <summary>
    /// 渲染后的主题.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 渲染后的正文.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 内容类型（Html 时邮件按 HTML 发送，其它渠道降级纯文本）.
    /// </summary>
    public NotificationContentType ContentType { get; init; } = NotificationContentType.Text;
}
