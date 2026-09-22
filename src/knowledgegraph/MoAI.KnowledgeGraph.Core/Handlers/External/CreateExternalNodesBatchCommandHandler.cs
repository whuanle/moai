using Maomi.MQ;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.External;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="CreateExternalNodesBatchCommand"/>
/// </summary>
public class CreateExternalNodesBatchCommandHandler : IRequestHandler<CreateExternalNodesBatchCommand, ExternalBatchResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IExternalKnowledgeGraphAuthorizer _externalAuthorizer;
    private readonly IKnowledgeGraphStore _store;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<CreateExternalNodesBatchCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateExternalNodesBatchCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="externalAuthorizer">外部授权器.</param>
    /// <param name="store">图存储.</param>
    /// <param name="messagePublisher">消息发布器.</param>
    /// <param name="logger">日志.</param>
    public CreateExternalNodesBatchCommandHandler(DatabaseContext databaseContext, IExternalKnowledgeGraphAuthorizer externalAuthorizer, IKnowledgeGraphStore store, IMessagePublisher messagePublisher, ILogger<CreateExternalNodesBatchCommandHandler> logger)
    {
        _databaseContext = databaseContext;
        _externalAuthorizer = externalAuthorizer;
        _store = store;
        _messagePublisher = messagePublisher;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ExternalBatchResponse> Handle(CreateExternalNodesBatchCommand request, CancellationToken cancellationToken)
    {
        await _externalAuthorizer.AuthorizeAsync(request.KnowledgeGraphId, request.Caller.TeamId, write: true, cancellationToken);

        // 逐条业务校验，任一失败整批拒绝（不触发图库写入）；名称非空/长度已由 Command Validate 保证.
        var typeIds = await _databaseContext.KnowledgeGraphEntityTypes
            .Where(x => x.KnowledgeGraphId == request.KnowledgeGraphId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var typeIdSet = typeIds.ToHashSet();

        for (var index = 0; index < request.Items.Count; index++)
        {
            var item = request.Items[index];
            if (!typeIdSet.Contains(item.EntityTypeId))
            {
                throw new BusinessException($"第 {index} 条校验失败：实体类型不存在.") { StatusCode = 400 };
            }
        }

        var nodes = request.Items
            .Select(x => new KnowledgeGraphNodeInput(x.EntityTypeId, x.Name, x.Description ?? string.Empty))
            .ToList();
        var records = await _store.CreateNodesBatchAsync(request.KnowledgeGraphId, nodes, cancellationToken);
        await KgEmbeddingDeltaPublisher.PublishNodesUpsertAsync(
            _messagePublisher,
            _logger,
            request.KnowledgeGraphId,
            records.Select(x => x.Id).ToList());

        return new ExternalBatchResponse
        {
            SuccessCount = records.Count,
            FailedCount = 0,
            Results = records.Select((record, index) => new ExternalBatchItemResult
            {
                Index = index,
                Ok = true,
                Id = record.Id,
                Message = null,
            }).ToList(),
        };
    }
}
