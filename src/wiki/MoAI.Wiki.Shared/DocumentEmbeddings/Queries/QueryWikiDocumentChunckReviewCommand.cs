using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.DocumentEmbeddings.Queries;

/// <summary>
/// 文件切片预览服务.
/// </summary>
public class QueryWikiDocumentChunckReviewCommand : PagedParamter, IRequest<QueryWikiDocumentChunckReviewCommandResponse>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 文档 id.
    /// </summary>
    public int DocumentId { get; init; }
}

public class QueryWikiDocumentChunckReviewCommandResponse
{
    public IReadOnlyCollection<string> Items { get; init; } = new List<string>();
}