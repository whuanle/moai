using MediatR;
using MoAI.Wiki.WebDocuments.Queries.Responses;

namespace MoAI.Wiki.WebDocuments.Queries;

public class QueryWikiConfigInfoCommand : IRequest<QueryWikiConfigInfoCommandResponse>
{
    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// id.
    /// </summary>
    public int WikiWebConfigId { get; init; }
}
