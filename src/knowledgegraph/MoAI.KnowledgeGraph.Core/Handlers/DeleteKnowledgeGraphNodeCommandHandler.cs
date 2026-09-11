using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteKnowledgeGraphNodeCommand"/>
/// </summary>
public class DeleteKnowledgeGraphNodeCommandHandler : IRequestHandler<DeleteKnowledgeGraphNodeCommand, EmptyCommandResponse>
{
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteKnowledgeGraphNodeCommandHandler"/> class.
    /// </summary>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="store">图存储.</param>
    public DeleteKnowledgeGraphNodeCommandHandler(IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphStore store)
    {
        _authorizer = authorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteKnowledgeGraphNodeCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeManagedAsync(request.KnowledgeGraphId, adminOnly: false, cancellationToken);
        var deleted = await _store.DeleteNodeAsync(request.KnowledgeGraphId, request.NodeId, cancellationToken);
        if (!deleted)
        {
            throw new BusinessException("节点不存在.") { StatusCode = 404 };
        }

        return EmptyCommandResponse.Default;
    }
}
