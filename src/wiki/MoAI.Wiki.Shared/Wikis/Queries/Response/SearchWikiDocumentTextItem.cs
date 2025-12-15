using Microsoft.KernelMemory;

namespace MoAI.Wiki.Wikis.Queries.Response;
public class SearchWikiDocumentTextItem
{
    /// <summary>
    /// 此 id 不是切割预览中的文本 id.
    /// </summary>
    public Guid ChunkId { get; init; }

    /// <summary>
    /// 被索引的文本.
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// 被召回的文本块.
    /// </summary>
    public string ChunkText { get; init; } = string.Empty;

    /// <summary>
    /// 相关度.
    /// </summary>
    public double RecordRelevance { get; init; }

    /// <summary>
    /// 所属文档.
    /// </summary>
    public int DocumentId { get; init; }

    /// <summary>
    /// 文件名称.
    /// </summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// 文件类型.
    /// </summary>
    public string FileType { get; init; } = string.Empty;
}
