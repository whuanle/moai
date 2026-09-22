using Maomi.MQ;
using MediatR;
using Microsoft.Extensions.Logging;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.External;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteExternalNodeCommand"/>
/// </summary>
public class DeleteExternalNodeCommandHandler : IRequestHandler<DeleteExternalNodeCommand, EmptyCommandResponse>
{
    private readonly IExternalKnowledgeGraphAuthorizer _externalAuthorizer;
    private readonly IKnowledgeGraphStore _store;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<DeleteExternalNodeCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteExternalNodeCommandHandler"/> class.
    /// </summary>
    /// <param name="externalAuthorizer">外部授权器.</param>
    /// <param name="store">图存储.</param>
    /// <param name="messagePublisher">消息发布器.</param>
    /// <param name="logger">日志.</param>
    public DeleteExternalNodeCommandHandler(IExternalKnowledgeGraphAuthorizer externalAuthorizer, IKnowledgeGraphStore store, IMessagePublisher messagePublisher, ILogger<DeleteExternalNodeCommandHandler> logger)
    {
        _externalAuthorizer = externalAuthorizer;
        _store = store;
        _messagePublisher = messagePublisher;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteExternalNodeCommand request, CancellationToken cancellationToken)
    {
        await _externalAuthorizer.AuthorizeAsync(request.KnowledgeGraphId, request.Caller.TeamId, write: true, cancellationToken);
        var deleted = await _store.DeleteNodeAsync(request.KnowledgeGraphId, request.NodeId, cancellationToken);
        if (!deleted)
        {
            throw new BusinessException("节点不存在.") { StatusCode = 404 };
        }

        await KgEmbeddingDeltaPublisher.PublishNodeDeleteAsync(_messagePublisher, _logger, request.KnowledgeGraphId, request.NodeId);
        return EmptyCommandResponse.Default;
    }
}
