namespace MoAI.Wiki.Queries.Responses;

/// <summary>
/// 知识库文档列表项（不含正文）.
/// </summary>
public class WikiDocumentItem
{
    /// <summary>
    /// 文档 id.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// 所属知识库 id.
    /// </summary>
    public long WikiId { get; set; }

    /// <summary>
    /// 文档标题.
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }
}
