namespace MoAI.Wiki.Queries.Responses;

/// <summary>
/// 知识库文档详情响应（含正文）.
/// </summary>
public class QueryWikiDocumentCommandResponse
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
    /// 文档内容（Markdown）.
    /// </summary>
    public string Content { get; set; } = default!;

    /// <summary>
    /// 我在所属团队中的角色：0=Owner 1=Admin 2=Member.
    /// </summary>
    public int MyRole { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }
}
