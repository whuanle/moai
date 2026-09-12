namespace MoAI.App.Queries.Responses;

/// <summary>
/// 会话消息响应.
/// </summary>
public class QueryAppSessionMessagesCommandResponse
{
    /// <summary>
    /// 会话 id.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// 会话标题.
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// 消息集合（按 seq 升序）.
    /// </summary>
    public IReadOnlyList<AppMessageItem> Items { get; set; } = new List<AppMessageItem>();
}
