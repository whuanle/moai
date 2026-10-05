namespace MoAI.Wiki.Models;

/// <summary>
/// 检索命中片段的相邻上下文片段.
/// </summary>
public class WikiSearchContextChunk
{
    /// <summary>
    /// 切片 id.
    /// </summary>
    public long ChunkId { get; set; }

    /// <summary>
    /// 切片在文档中的序号（从 0 开始）.
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// 切片内容.
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
