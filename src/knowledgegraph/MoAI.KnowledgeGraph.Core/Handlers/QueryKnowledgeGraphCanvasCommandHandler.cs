using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Models;
using MoAI.KnowledgeGraph.Queries;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="QueryKnowledgeGraphCanvasCommand"/>
/// </summary>
public class QueryKnowledgeGraphCanvasCommandHandler : IRequestHandler<QueryKnowledgeGraphCanvasCommand, QueryKnowledgeGraphCanvasCommandResponse>
{
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryKnowledgeGraphCanvasCommandHandler"/> class.
    /// </summary>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="store">图存储.</param>
    public QueryKnowledgeGraphCanvasCommandHandler(IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphStore store)
    {
        _authorizer = authorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<QueryKnowledgeGraphCanvasCommandResponse> Handle(QueryKnowledgeGraphCanvasCommand request, CancellationToken cancellationToken)
    {
        var (graph, _) = await _authorizer.AuthorizeAsync(request.KnowledgeGraphId, adminOnly: false, cancellationToken);
        if (!string.Equals(graph.Mode, KnowledgeGraphModes.Managed, StringComparison.Ordinal))
        {
            throw new BusinessException("外部接入图谱暂不支持画布视图.") { StatusCode = 409 };
        }

        var limit = request.Limit < 1 ? 200 : Math.Min(request.Limit, 500);
        var (nodes, edges, truncated) = await _store.QueryCanvasAsync(
            request.KnowledgeGraphId, request.EntityTypeId, request.RelationTypeId, request.Keyword, limit, cancellationToken);

        return new QueryKnowledgeGraphCanvasCommandResponse
        {
            Nodes = nodes.Select(x => new KnowledgeGraphNodeItem
            {
                NodeId = x.Id,
                EntityTypeId = x.EntityTypeId,
                Name = x.Name,
                Description = x.Description,
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
