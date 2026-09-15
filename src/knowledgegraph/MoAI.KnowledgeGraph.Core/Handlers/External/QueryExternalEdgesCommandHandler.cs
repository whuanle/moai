using MediatR;
using MoAI.KnowledgeGraph.External;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="QueryExternalEdgesCommand"/>
/// </summary>
public class QueryExternalEdgesCommandHandler : IRequestHandler<QueryExternalEdgesCommand, QueryKnowledgeGraphEdgesCommandResponse>
{
    private readonly IExternalKnowledgeGraphAuthorizer _externalAuthorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryExternalEdgesCommandHandler"/> class.
    /// </summary>
    /// <param name="externalAuthorizer">外部授权器.</param>
    /// <param name="store">图存储.</param>
    public QueryExternalEdgesCommandHandler(IExternalKnowledgeGraphAuthorizer externalAuthorizer, IKnowledgeGraphStore store)
    {
        _externalAuthorizer = externalAuthorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<QueryKnowledgeGraphEdgesCommandResponse> Handle(QueryExternalEdgesCommand request, CancellationToken cancellationToken)
    {
        await _externalAuthorizer.AuthorizeAsync(request.KnowledgeGraphId, request.Caller.TeamId, write: false, cancellationToken);
        var pageNo = request.PageNo < 1 ? 1 : request.PageNo;
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var (items, total) = await _store.ListEdgesAsync(request.KnowledgeGraphId, request.RelationTypeId, request.NodeId, pageNo, pageSize, cancellationToken);
        return new QueryKnowledgeGraphEdgesCommandResponse
        {
            Total = total,
            Items = items.Select(x => new KnowledgeGraphEdgeItem
            {
                EdgeId = x.Id,
                RelationTypeId = x.RelationTypeId,
                SourceNodeId = x.SourceNodeId,
                TargetNodeId = x.TargetNodeId,
            }).ToList(),
        };
    }
}
