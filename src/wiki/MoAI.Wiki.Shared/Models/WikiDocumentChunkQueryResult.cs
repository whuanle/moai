namespace MoAI.Wiki.Models;

/// <summary>
/// 按文档序号批量获取切片的查询结果.
/// </summary>
public class WikiDocumentChunkQueryResult
{
    /// <summary>
    /// 文档 id.
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// 文档名称.
    /// </summary>
    public string DocumentName { get; set; } = string.Empty;

    /// <summary>
    /// 命中的切片集合（按序号升序）.
    /// </summary>
    public IReadOnlyList<WikiChunkContent> Chunks { get; set; } = [];
}
