using MoAI.Infra.Models;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Documents.Queries.Responses;

/// <summary>
/// 文档列表.
/// </summary>
public class WikiDocumentTaskItem : AuditsInfo
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
    public FileEmbeddingState State { get; set; }

    /// <summary>
    /// 执行信息.
    /// </summary>
    public string Message { get; set; } = default!;

    /// <summary>
    /// 每段最大token数量.
    /// </summary>
    public int MaxTokensPerParagraph { get; set; }

    /// <summary>
    /// 重叠的token数量.
    /// </summary>
    public int OverlappingTokens { get; set; }

    /// <summary>
    /// 分词器.
    /// </summary>
    public string Tokenizer { get; set; } = default!;
}