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
    public int DocumentId { get; init; }

    public string FileName { get; init; }

    public int FileSize { get; init; }

    public string ContentType { get; init; }

    public DocumentTextPartionConfig PartionConfig { get; init; }
}