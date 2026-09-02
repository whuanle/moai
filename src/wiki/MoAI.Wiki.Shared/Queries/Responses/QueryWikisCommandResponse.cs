namespace MoAI.Wiki.Queries.Responses;

/// <summary>
/// 知识库列表响应.
/// </summary>
public class QueryWikisCommandResponse
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; set; }

    /// <summary>
    /// 我在该团队中的角色：0=Owner 1=Admin 2=Member.
    /// </summary>
    public int MyRole { get; set; }

    /// <summary>
    /// 知识库集合.
    /// </summary>
    public IReadOnlyList<WikiItem> Items { get; set; } = new List<WikiItem>();
}
