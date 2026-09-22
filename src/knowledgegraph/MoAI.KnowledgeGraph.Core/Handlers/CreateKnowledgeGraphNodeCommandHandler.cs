using Maomi.MQ;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="CreateKnowledgeGraphNodeCommand"/>
/// </summary>
public class CreateKnowledgeGraphNodeCommandHandler : IRequestHandler<CreateKnowledgeGraphNodeCommand, SimpleString>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphStore _store;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<CreateKnowledgeGraphNodeCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateKnowledgeGraphNodeCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="store">图存储.</param>
    /// <param name="messagePublisher">消息发布器.</param>
    /// <param name="logger">日志.</param>
    public CreateKnowledgeGraphNodeCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphStore store, IMessagePublisher messagePublisher, ILogger<CreateKnowledgeGraphNodeCommandHandler> logger)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _store = store;
        _messagePublisher = messagePublisher;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SimpleString> Handle(CreateKnowledgeGraphNodeCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeManagedAsync(request.KnowledgeGraphId, adminOnly: true, cancellationToken);

        var typeExists = await _databaseContext.KnowledgeGraphEntityTypes
            .AnyAsync(x => x.Id == request.EntityTypeId && x.KnowledgeGraphId == request.KnowledgeGraphId, cancellationToken);
        if (!typeExists)
        {
            throw new BusinessException("实体类型不存在.") { StatusCode = 400 };
        }

        var propsJson = KnowledgeGraphPropertyJson.WriteValues(request.Properties ?? new Dictionary<string, string>(StringComparer.Ordinal));
        var node = await _store.CreateNodeAsync(request.KnowledgeGraphId, request.EntityTypeId, request.Name, request.Description ?? string.Empty, propsJson, cancellationToken);
        await KgEmbeddingDeltaPublisher.PublishNodeUpsertAsync(_messagePublisher, _logger, request.KnowledgeGraphId, node.Id);
        return new SimpleString { Value = node.Id };
    }
}
