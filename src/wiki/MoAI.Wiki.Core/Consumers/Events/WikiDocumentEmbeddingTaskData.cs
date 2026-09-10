namespace MoAI.Wiki.Consumers.Events;

/// <summary>
/// 知识库文档向量化任务数据.
/// </summary>
public class WikiDocumentEmbeddingTaskData
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 文档 id.
    /// </summary>
    public int DocumentId { get; init; }

    /// <summary>
    /// 是否对原文切片内容向量化.
    /// </summary>
    public bool IsEmbedSourceText { get; init; }

    /// <summary>
    /// 是否对元数据向量化.
    /// </summary>
    public bool IsEmbedMetadata { get; init; }
}