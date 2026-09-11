using MediatR;
using MoAI.KnowledgeGraph.Queries;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="QueryKnowledgeGraphEdgesCommand"/>
/// </summary>
public class QueryKnowledgeGraphEdgesCommandHandler : IRequestHandler<QueryKnowledgeGraphEdgesCommand, QueryKnowledgeGraphEdgesCommandResponse>
{
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryKnowledgeGraphEdgesCommandHandler"/> class.
    /// </summary>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="store">图存储.</param>
    public QueryKnowledgeGraphEdgesCommandHandler(IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphStore store)
    {
        _authorizer = authorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<QueryKnowledgeGraphEdgesCommandResponse> Handle(QueryKnowledgeGraphEdgesCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeAsync(request.KnowledgeGraphId, adminOnly: false, cancellationToken);
        var pageNo = request.PageNo < 1 ? 1 : request.PageNo;
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

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
