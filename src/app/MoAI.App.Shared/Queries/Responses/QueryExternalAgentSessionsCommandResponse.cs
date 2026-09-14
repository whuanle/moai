namespace MoAI.App.Queries.Responses;

/// <summary>
/// 外部用户会话列表响应.
/// </summary>
public class QueryExternalAgentSessionsCommandResponse
{
    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 会话集合（按最后消息时间倒序）.
    /// </summary>
    public IReadOnlyList<AppSessionItem> Items { get; init; } = new List<AppSessionItem>();
}
