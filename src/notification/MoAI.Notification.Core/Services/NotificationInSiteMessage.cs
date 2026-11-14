using MoAI.Message.Models;
using MoAI.Notification.Models;
using MoAI.Notification.Senders;

namespace MoAI.Notification.Services;

/// <summary>
/// 站内信条目构造：把统一通知消息转为站内信发送内容（HTML 模板自动降级为去标签纯文本），
/// 广播的「发送站内信」选项与领域服务的「站内信托底」共用同一构造与降级规则.
/// </summary>
public static class NotificationInSiteMessage
{
    /// <summary>
    /// 来源兜底标识（消息未指定 Source 时使用）.
    /// </summary>
    public const string DefaultSource = "notification";

    /// <summary>
    /// 构造站内信条目（入参应为模板渲染后的消息，站内信使用渲染后文本）.
    /// </summary>
    /// <param name="message">通知消息.</param>
    /// <returns>返回站内信发送内容.</returns>
    public static MessageSendItem Create(NotificationMessage message)
    {
        return new MessageSendItem
        {
            Title = message.Title,
            Content = NotificationText.ContentText(message),
            MessageType = MessageType.System,
            Source = message.Source.Length == 0 ? DefaultSource : message.Source
        };
    }
}
