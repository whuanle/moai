using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Embeddings.Models;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Documents.Queries;

/// <summary>
/// 查询单个文档信息.
/// </summary>
public class QueryWikiDocumentInfoCommandResponse : AuditsInfo
{
    /// <summary>
    /// 文档 id.
    /// </summary>
    public int DocumentId { get; init; }

    /// <summary>
    /// 文件名称.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// 文件大小.
    /// </summary>
    public int FileSize { get; init; }

    /// <summary>
    /// 文件类型.
    /// </summary>
    public string ContentType { get; init; } = string.Empty;

    /// <summary>
    /// 是否有向量化内容.
    /// </summary>
    public bool IsEmbedding { get; init; }

    /// <summary>
    /// 切片数量.
    /// </summary>
    public int ChunkCount { get; set; }

    /// <summary>
    /// 元数据数量.
    /// </summary>
    public int MetedataCount { get; set; }

    public DocumentTextPartionConfig PartionConfig { get; init; }
}