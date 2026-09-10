namespace MoAI.Wiki.Queries.Responses;

/// <summary>
/// 知识库文档列表响应.
/// </summary>
public class QueryWikiDocumentsCommandResponse
{
    /// <summary>
    /// 文档集合.
    /// </summary>
    public IReadOnlyList<WikiDocumentItem> Items { get; set; } = new List<WikiDocumentItem>();

    /// <summary>
    /// 文档总数.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// 页码（从 1 开始）.
    /// </summary>
    public int PageNo { get; set; }

    /// <summary>
    /// 每页大小.
    /// </summary>
    public int PageSize { get; set; }
}
