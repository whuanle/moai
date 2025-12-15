using MoAI.Infra.Models;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Documents.Models;

/// <summary>
/// 文档列表.
/// </summary>
public class WikiDocumentEmbeddingTaskItem : AuditsInfo
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; set; }

    /// <summary>
    /// 文档id.
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// 文件id.
    /// </summary>
    public int FileId { get; set; }

    /// <summary>
    /// 文件名称.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// 文件大小.
    /// </summary>
    public long FileSize { get; init; }

    /// <summary>
    /// 文件类型.
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// 任务状态.
    /// </summary>
    public WorkerState State { get; set; }

    /// <summary>
    /// 执行信息.
    /// </summary>
    public string Message { get; set; } = default!;
}