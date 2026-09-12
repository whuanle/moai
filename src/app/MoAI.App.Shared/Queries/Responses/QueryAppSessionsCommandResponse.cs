namespace MoAI.App.Queries.Responses;

/// <summary>
/// 会话列表响应.
/// </summary>
public class QueryAppSessionsCommandResponse
{
    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; set; }

    /// <summary>
    /// 我在该团队中的角色：0=Member 1=Admin 2=Owner.
    /// </summary>
    public int MyRole { get; set; }

    /// <summary>
    /// 会话集合（按最后消息时间倒序）.
    /// </summary>
    public IReadOnlyList<AppSessionItem> Items { get; set; } = new List<AppSessionItem>();
}
