using MediatR;
using MoAI.Wiki.DocumentEmbedding.Models;

namespace MoAI.Wiki.DocumentEmbeddings.Queries;

/// <summary>
/// 查询知识库文档被切割的块.
/// </summary>
public class QueryWikiDocumentChunksCommand : IRequest<QueryWikiDocumentChunksCommandResponse>
{
    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int DocumentId { get; init; }
}
