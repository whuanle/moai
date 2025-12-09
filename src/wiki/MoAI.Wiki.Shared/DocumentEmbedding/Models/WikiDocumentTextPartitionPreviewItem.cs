namespace MoAI.Wiki.DocumentEmbedding.Models;

/// <summary>
/// 文档内容.
/// </summary>
public class WikiDocumentTextPartitionPreviewItem
{
    /// <summary>
    /// 切片 id.
    /// </summary>
    public long ChunkId { get; init; }

    /// <summary>
    /// 分块顺序.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// 分块文本.
    /// </summary>
    public string Text { get; set; } = string.Empty;
}