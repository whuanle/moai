using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.External;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="QueryExternalEdgeCommand"/>
/// </summary>
public class QueryExternalEdgeCommandHandler : IRequestHandler<QueryExternalEdgeCommand, QueryKnowledgeGraphEdgeCommandResponse>
{
    private readonly IExternalKnowledgeGraphAuthorizer _externalAuthorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryExternalEdgeCommandHandler"/> class.
    /// </summary>
    /// <param name="externalAuthorizer">外部授权器.</param>
    /// <param name="store">图存储.</param>
    public QueryExternalEdgeCommandHandler(IExternalKnowledgeGraphAuthorizer externalAuthorizer, IKnowledgeGraphStore store)
    {
        _externalAuthorizer = externalAuthorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<QueryKnowledgeGraphEdgeCommandResponse> Handle(QueryExternalEdgeCommand request, CancellationToken cancellationToken)
    {
        await _externalAuthorizer.AuthorizeAsync(request.KnowledgeGraphId, request.Caller.TeamId, write: false, cancellationToken);
        var edge = await _store.GetEdgeAsync(request.KnowledgeGraphId, request.EdgeId, cancellationToken)
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
