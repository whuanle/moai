using Maomi.MQ;
using MediatR;
using Microsoft.Extensions.Logging;
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
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<DeleteKnowledgeGraphNodeCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteKnowledgeGraphNodeCommandHandler"/> class.
    /// </summary>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="store">图存储.</param>
    /// <param name="messagePublisher">消息发布器.</param>
    /// <param name="logger">日志.</param>
    public DeleteKnowledgeGraphNodeCommandHandler(IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphStore store, IMessagePublisher messagePublisher, ILogger<DeleteKnowledgeGraphNodeCommandHandler> logger)
    {
        _authorizer = authorizer;
        _store = store;
        _messagePublisher = messagePublisher;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteKnowledgeGraphNodeCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeManagedAsync(request.KnowledgeGraphId, adminOnly: true, cancellationToken);
        var deleted = await _store.DeleteNodeAsync(request.KnowledgeGraphId, request.NodeId, cancellationToken);
        if (!deleted)
        {
            throw new BusinessException("节点不存在.") { StatusCode = 404 };
        }

        await KgEmbeddingDeltaPublisher.PublishNodeDeleteAsync(_messagePublisher, _logger, request.KnowledgeGraphId, request.NodeId);
        return EmptyCommandResponse.Default;
    }
}
