namespace MoAI.Message.Models;

/// <summary>
/// SignalR 事件推送的事件名，小写点分（onEvent 事件的 event 字段取值）.
/// </summary>
public static class EventNames
{
    /// <summary>
    /// 收到新的站内信：客户端收到后应调用 /api/message/unread-count 与 /api/message/list 刷新，事件不携带正文.
    /// </summary>
    public const string MessageNew = "message.new";
}
