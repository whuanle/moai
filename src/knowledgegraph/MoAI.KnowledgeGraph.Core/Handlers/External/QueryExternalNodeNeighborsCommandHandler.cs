using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.External;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="QueryExternalNodeNeighborsCommand"/>
/// </summary>
public class QueryExternalNodeNeighborsCommandHandler : IRequestHandler<QueryExternalNodeNeighborsCommand, QueryKnowledgeGraphCanvasCommandResponse>
{
    private readonly IExternalKnowledgeGraphAuthorizer _externalAuthorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryExternalNodeNeighborsCommandHandler"/> class.
    /// </summary>
    /// <param name="externalAuthorizer">外部授权器.</param>
    /// <param name="store">图存储.</param>
    public QueryExternalNodeNeighborsCommandHandler(IExternalKnowledgeGraphAuthorizer externalAuthorizer, IKnowledgeGraphStore store)
    {
        _externalAuthorizer = externalAuthorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<QueryKnowledgeGraphCanvasCommandResponse> Handle(QueryExternalNodeNeighborsCommand request, CancellationToken cancellationToken)
    {
        await _externalAuthorizer.AuthorizeAsync(request.KnowledgeGraphId, request.Caller.TeamId, write: false, cancellationToken);
        var limit = request.Limit < 1 ? 100 : Math.Min(request.Limit, 500);

        // 外部接口只面向托管图：先校验节点属于该图谱，再做一跳邻接展开.
        _ = await _store.GetNodeAsync(request.KnowledgeGraphId, request.NodeId, cancellationToken)
            ?? throw new BusinessException("节点不存在.") { StatusCode = 404 };

        var (nodes, edges, truncated) = await _store.GetNeighborsAsync(request.KnowledgeGraphId, request.NodeId, limit, cancellationToken);

        return new QueryKnowledgeGraphCanvasCommandResponse
        {
            Nodes = nodes.Select(x => new KnowledgeGraphNodeItem
            {
                NodeId = x.Id,
                EntityTypeId = x.EntityTypeId,
                Name = x.Name,
                Description = x.Description,
                Properties = KnowledgeGraphPropertyJson.ParseValues(x.PropsJson),
            }).ToList(),
            Edges = edges.Select(x => new KnowledgeGraphEdgeItem
            {
                EdgeId = x.Id,
                RelationTypeId = x.RelationTypeId,
                SourceNodeId = x.SourceNodeId,
                TargetNodeId = x.TargetNodeId,
            }).ToList(),
            Truncated = truncated,
        };
    }
}
