using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteKnowledgeGraphEdgeCommand"/>
/// </summary>
public class DeleteKnowledgeGraphEdgeCommandHandler : IRequestHandler<DeleteKnowledgeGraphEdgeCommand, EmptyCommandResponse>
{
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteKnowledgeGraphEdgeCommandHandler"/> class.
    /// </summary>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="store">图存储.</param>
    public DeleteKnowledgeGraphEdgeCommandHandler(IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphStore store)
    {
        _authorizer = authorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteKnowledgeGraphEdgeCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeManagedAsync(request.KnowledgeGraphId, adminOnly: true, cancellationToken);
        var deleted = await _store.DeleteEdgeAsync(request.KnowledgeGraphId, request.EdgeId, cancellationToken);
        if (!deleted)
        {
            throw new BusinessException("边不存在.") { StatusCode = 404 };
        }

        return EmptyCommandResponse.Default;
    }
}
