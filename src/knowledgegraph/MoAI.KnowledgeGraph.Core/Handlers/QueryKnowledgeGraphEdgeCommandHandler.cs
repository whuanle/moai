using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Queries;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="QueryKnowledgeGraphEdgeCommand"/>
/// </summary>
public class QueryKnowledgeGraphEdgeCommandHandler : IRequestHandler<QueryKnowledgeGraphEdgeCommand, QueryKnowledgeGraphEdgeCommandResponse>
{
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryKnowledgeGraphEdgeCommandHandler"/> class.
    /// </summary>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="store">图存储.</param>
    public QueryKnowledgeGraphEdgeCommandHandler(IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphStore store)
    {
        _authorizer = authorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<QueryKnowledgeGraphEdgeCommandResponse> Handle(QueryKnowledgeGraphEdgeCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeAsync(request.KgId, adminOnly: false, cancellationToken);
        var edge = await _store.GetEdgeAsync(request.KgId, request.EdgeId, cancellationToken)
            ?? throw new BusinessException("边不存在.") { StatusCode = 404 };

        return new QueryKnowledgeGraphEdgeCommandResponse
        {
            EdgeId = edge.Id,
            RelationTypeId = edge.RelationTypeId,
            SourceNodeId = edge.SourceNodeId,
            TargetNodeId = edge.TargetNodeId,
        };
    }
}
