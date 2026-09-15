using MediatR;
using MoAI.KnowledgeGraph.External;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="QueryExternalNodesCommand"/>
/// </summary>
public class QueryExternalNodesCommandHandler : IRequestHandler<QueryExternalNodesCommand, QueryKnowledgeGraphNodesCommandResponse>
{
    private readonly IExternalKnowledgeGraphAuthorizer _externalAuthorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryExternalNodesCommandHandler"/> class.
    /// </summary>
    /// <param name="externalAuthorizer">外部授权器.</param>
    /// <param name="store">图存储.</param>
    public QueryExternalNodesCommandHandler(IExternalKnowledgeGraphAuthorizer externalAuthorizer, IKnowledgeGraphStore store)
    {
        _externalAuthorizer = externalAuthorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<QueryKnowledgeGraphNodesCommandResponse> Handle(QueryExternalNodesCommand request, CancellationToken cancellationToken)
    {
        await _externalAuthorizer.AuthorizeAsync(request.KnowledgeGraphId, request.Caller.TeamId, write: false, cancellationToken);
        var pageNo = request.PageNo < 1 ? 1 : request.PageNo;
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var (items, total) = await _store.ListNodesAsync(request.KnowledgeGraphId, request.EntityTypeId, request.Keyword, pageNo, pageSize, cancellationToken);
        return new QueryKnowledgeGraphNodesCommandResponse
        {
            Total = total,
            Items = items.Select(x => new KnowledgeGraphNodeItem
            {
                NodeId = x.Id,
                EntityTypeId = x.EntityTypeId,
                Name = x.Name,
                Description = x.Description,
                Properties = KnowledgeGraphPropertyJson.ParseValues(x.PropsJson),
            }).ToList(),
        };
    }
}
