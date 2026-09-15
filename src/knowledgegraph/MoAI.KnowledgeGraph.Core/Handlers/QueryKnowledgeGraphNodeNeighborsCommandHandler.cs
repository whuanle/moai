using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Models;
using MoAI.KnowledgeGraph.Queries;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="QueryKnowledgeGraphNodeNeighborsCommand"/>
/// </summary>
public class QueryKnowledgeGraphNodeNeighborsCommandHandler : IRequestHandler<QueryKnowledgeGraphNodeNeighborsCommand, QueryKnowledgeGraphCanvasCommandResponse>
{
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryKnowledgeGraphNodeNeighborsCommandHandler"/> class.
    /// </summary>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="store">图存储.</param>
    public QueryKnowledgeGraphNodeNeighborsCommandHandler(IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphStore store)
    {
        _authorizer = authorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<QueryKnowledgeGraphCanvasCommandResponse> Handle(QueryKnowledgeGraphNodeNeighborsCommand request, CancellationToken cancellationToken)
    {
        var (graph, _) = await _authorizer.AuthorizeAsync(request.KnowledgeGraphId, adminOnly: false, cancellationToken);
        var limit = request.Limit < 1 ? 100 : Math.Min(request.Limit, 500);

        if (string.Equals(graph.Mode, KnowledgeGraphModes.Connected, StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(graph.Database))
            {
                throw new BusinessException("接入图谱缺少数据库配置.") { StatusCode = 409 };
            }

            // 先判定节点存在（孤立节点无邻居也应返回空画布而非 404），再展开一跳邻接.
            _ = await _store.GetConnectedNodeAsync(graph.Database, request.NodeId, cancellationToken)
                ?? throw new BusinessException("节点不存在.") { StatusCode = 404 };

            var (nodes, edges, truncated) = await _store.GetConnectedNeighborsAsync(graph.Database, request.NodeId, limit, cancellationToken);

            return new QueryKnowledgeGraphCanvasCommandResponse
            {
                Nodes = nodes.Select(x => new KnowledgeGraphNodeItem
                {
                    NodeId = x.Id,
                    EntityLabel = x.Label,
                    Name = x.Name,
                    Description = x.Description,
                }).ToList(),
                Edges = edges.Select(x => new KnowledgeGraphEdgeItem
                {
                    EdgeId = x.Id,
                    RelationName = x.RelationType,
                    SourceNodeId = x.SourceNodeId,
                    TargetNodeId = x.TargetNodeId,
                }).ToList(),
                Truncated = truncated,
            };
        }

        _ = await _store.GetNodeAsync(request.KnowledgeGraphId, request.NodeId, cancellationToken)
            ?? throw new BusinessException("节点不存在.") { StatusCode = 404 };

        var (managedNodes, managedEdges, managedTruncated) = await _store.GetNeighborsAsync(request.KnowledgeGraphId, request.NodeId, limit, cancellationToken);

        return new QueryKnowledgeGraphCanvasCommandResponse
        {
            Nodes = managedNodes.Select(x => new KnowledgeGraphNodeItem
            {
                NodeId = x.Id,
                EntityTypeId = x.EntityTypeId,
                Name = x.Name,
                Description = x.Description,
                Properties = KnowledgeGraphPropertyJson.ParseValues(x.PropsJson),
            }).ToList(),
            Edges = managedEdges.Select(x => new KnowledgeGraphEdgeItem
            {
                EdgeId = x.Id,
                RelationTypeId = x.RelationTypeId,
                SourceNodeId = x.SourceNodeId,
                TargetNodeId = x.TargetNodeId,
            }).ToList(),
            Truncated = managedTruncated,
        };
    }
}
