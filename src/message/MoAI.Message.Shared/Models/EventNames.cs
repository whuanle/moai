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

    /// <summary>
    /// 登录态已吊销：禁用/启用/强制下线/改密后推送，客户端应清空本地登录态并回到登录页；
    /// data 仅带 reason 与 authVersion，不携带业务数据.
    /// </summary>
    public const string AuthRevoked = "auth.revoked";
}
