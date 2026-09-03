namespace MoAI.Wiki.Queries.Responses;

/// <summary>
/// 知识库文档列表响应.
/// </summary>
public class QueryWikiDocumentsCommandResponse
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public long WikiId { get; set; }

    /// <summary>
    /// 我在该团队中的角色：0=Owner 1=Admin 2=Member.
    /// </summary>
    public int MyRole { get; set; }

    /// <summary>
    /// 文档集合.
    /// </summary>
    public IReadOnlyList<WikiDocumentItem> Items { get; set; } = new List<WikiDocumentItem>();
}
