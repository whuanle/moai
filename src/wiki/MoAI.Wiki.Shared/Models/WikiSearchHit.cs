namespace MoAI.Wiki.Models;

/// <summary>
/// 知识库检索命中项.
/// </summary>
public class WikiSearchHit
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; set; }

    /// <summary>
    /// 文档 id.
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// 文档名称.
    /// </summary>
    public string DocumentName { get; set; } = string.Empty;

    /// <summary>
    /// 切片 id.
    /// </summary>
    public long ChunkId { get; set; }

    /// <summary>
    /// 切片内容.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 相似度得分（越大越相似）.
    /// </summary>
    public double? Score { get; set; }
}
