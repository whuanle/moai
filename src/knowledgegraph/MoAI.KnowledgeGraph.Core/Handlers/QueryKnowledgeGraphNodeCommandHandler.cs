using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Queries;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="QueryKnowledgeGraphNodeCommand"/>
/// </summary>
public class QueryKnowledgeGraphNodeCommandHandler : IRequestHandler<QueryKnowledgeGraphNodeCommand, QueryKnowledgeGraphNodeCommandResponse>
{
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryKnowledgeGraphNodeCommandHandler"/> class.
    /// </summary>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="store">图存储.</param>
    public QueryKnowledgeGraphNodeCommandHandler(IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphStore store)
    {
        _authorizer = authorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<QueryKnowledgeGraphNodeCommandResponse> Handle(QueryKnowledgeGraphNodeCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeAsync(request.KnowledgeGraphId, adminOnly: false, cancellationToken);
        var node = await _store.GetNodeAsync(request.KnowledgeGraphId, request.NodeId, cancellationToken)
            ?? throw new BusinessException("节点不存在.") { StatusCode = 404 };

        return new QueryKnowledgeGraphNodeCommandResponse
        {
            NodeId = node.Id,
            EntityTypeId = node.EntityTypeId,
            Name = node.Name,
            Description = node.Description,
        };
    }
}
