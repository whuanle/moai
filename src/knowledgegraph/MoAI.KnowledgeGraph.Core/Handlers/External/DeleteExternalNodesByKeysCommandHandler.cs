using Maomi.MQ;
using MediatR;
using Microsoft.Extensions.Logging;
using MoAI.KnowledgeGraph.External;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteExternalNodesByKeysCommand"/>
/// 按业务 key 批量删除节点（连带其边），并发布一次节点向量删除增量；不存在的 key 忽略.
/// </summary>
public class DeleteExternalNodesByKeysCommandHandler : IRequestHandler<DeleteExternalNodesByKeysCommand, DeleteExternalNodesByKeysResponse>
{
    private readonly IExternalKnowledgeGraphAuthorizer _externalAuthorizer;
    private readonly IKnowledgeGraphStore _store;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<DeleteExternalNodesByKeysCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteExternalNodesByKeysCommandHandler"/> class.
    /// </summary>
    /// <param name="externalAuthorizer">外部授权器.</param>
    /// <param name="store">图存储.</param>
    /// <param name="messagePublisher">消息发布器.</param>
    /// <param name="logger">日志.</param>
    public DeleteExternalNodesByKeysCommandHandler(IExternalKnowledgeGraphAuthorizer externalAuthorizer, IKnowledgeGraphStore store, IMessagePublisher messagePublisher, ILogger<DeleteExternalNodesByKeysCommandHandler> logger)
    {
        _externalAuthorizer = externalAuthorizer;
        _store = store;
        _messagePublisher = messagePublisher;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<DeleteExternalNodesByKeysResponse> Handle(DeleteExternalNodesByKeysCommand request, CancellationToken cancellationToken)
    {
        await _externalAuthorizer.AuthorizeAsync(request.KnowledgeGraphId, request.Caller.TeamId, write: true, cancellationToken);

        var keys = request.Keys
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var deletedIds = await _store.DeleteNodesByKeysAsync(request.KnowledgeGraphId, keys, cancellationToken);
        if (deletedIds.Count > 0)
        {
            await KgEmbeddingDeltaPublisher.PublishNodesDeleteAsync(_messagePublisher, _logger, request.KnowledgeGraphId, deletedIds);
        }

        return new DeleteExternalNodesByKeysResponse { DeletedCount = deletedIds.Count };
    }
}
