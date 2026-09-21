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
    /// 命中内容的元数据类型：0=原文切片 1=大纲 2=问题 3=关键词 4=摘要 5=聚合段.
    /// </summary>
    public int MetadataType { get; set; }

    /// <summary>
    /// 切片内容.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 相似度得分（越大越相似）.
    /// </summary>
    public double? Score { get; set; }
}
