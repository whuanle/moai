namespace MoAI.App.Queries.Responses;

/// <summary>
/// 应用列表响应.
/// </summary>
public class QueryAppsCommandResponse
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; set; }

    /// <summary>
    /// 我在该团队中的角色：0=Member 1=Admin 2=Owner.
    /// </summary>
    public int MyRole { get; set; }

    /// <summary>
    /// 应用集合.
    /// </summary>
    public IReadOnlyList<AppItem> Items { get; set; } = new List<AppItem>();
}
