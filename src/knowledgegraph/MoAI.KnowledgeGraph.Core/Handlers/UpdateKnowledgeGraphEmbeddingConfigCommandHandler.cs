using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maomi.MQ;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Models;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateKnowledgeGraphEmbeddingConfigCommand"/>
/// </summary>
public class UpdateKnowledgeGraphEmbeddingConfigCommandHandler : IRequestHandler<UpdateKnowledgeGraphEmbeddingConfigCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphStore _store;
    private readonly IKgEmbeddingVectorStore _vectorStore;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<UpdateKnowledgeGraphEmbeddingConfigCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateKnowledgeGraphEmbeddingConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="store">图存储.</param>
    /// <param name="vectorStore">向量存储.</param>
    /// <param name="messagePublisher">消息发布器.</param>
    /// <param name="logger">日志.</param>
    public UpdateKnowledgeGraphEmbeddingConfigCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphStore store, IKgEmbeddingVectorStore vectorStore, IMessagePublisher messagePublisher, ILogger<UpdateKnowledgeGraphEmbeddingConfigCommandHandler> logger)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _store = store;
        _vectorStore = vectorStore;
        _messagePublisher = messagePublisher;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateKnowledgeGraphEmbeddingConfigCommand request, CancellationToken cancellationToken)
    {
        var (graph, _) = await _authorizer.AuthorizeAsync(request.KnowledgeGraphId, adminOnly: true, cancellationToken);
        if (!string.Equals(graph.Mode, KnowledgeGraphModes.Managed, StringComparison.Ordinal))
        {
            throw new BusinessException("外部接入图谱不支持向量检索配置.") { StatusCode = 409 };
        }

        var authorizedModelIds = await _databaseContext.AiModelAuthorizations
            .Where(x => x.TeamId == graph.TeamId)
            .Select(x => x.AiModelId)
            .ToListAsync(cancellationToken);

        var model = await (from m in _databaseContext.AiModels
                           join c in _databaseContext.AiChannels on m.ChannelId equals c.Id
                           where m.Id == request.EmbeddingModelId && m.Enabled && c.Enabled && m.IsDeleted == 0 && c.IsDeleted == 0
                               && (m.IsPublic || authorizedModelIds.Contains(m.Id))
                           select new { m.Id, m.ModelKind })
            .FirstOrDefaultAsync(cancellationToken);
        if (model == null)
        {
            throw new BusinessException("向量化模型不存在、未启用或未授权给该团队.") { StatusCode = 400 };
        }

        // ModelKind 大小写归一在内存判断（镜像 UpdateWikiEmbeddingCommandHandler 的写法）
        if (!string.Equals(model.ModelKind, "embedding", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("所选模型不是向量化模型.") { StatusCode = 400 };
        }

        // 保存前记录旧配置，落库后据此决定清理旧向量与全量重嵌
        var oldModelId = graph.EmbeddingModelId;
        var oldDimensions = graph.EmbeddingDimensions;

        graph.EmbeddingModelId = request.EmbeddingModelId;
        graph.EmbeddingDimensions = request.EmbeddingDimensions;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        // 落库后的清理与全量重嵌均为 best effort：失败仅记日志，不回滚已保存的配置
        // （Guid? 判空用 == null，禁止 == Guid.Empty 哨兵）
        var hadConfig = oldModelId != null && oldDimensions > 0;
        var configChanged = hadConfig && (oldModelId!.Value != request.EmbeddingModelId || oldDimensions != request.EmbeddingDimensions);
        if (configChanged)
        {
            // 旧模型/维度的向量已与新维度不匹配：先整集合删除，防消费侧写入裸异常
            try
            {
                await _vectorStore.DeleteGraphVectorsAsync(graph.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理旧图谱向量集合失败. KgId={KgId}", graph.Id);
            }
        }

        try
        {
            // 首次配置与变更配置都发全量 delta：让已有节点立即可检索（消费侧静默跳过未配置场景）
            var nodeIds = await _store.GetNodeIdsAsync(graph.Id, cancellationToken);
            if (nodeIds.Count > 0)
            {
                await KgEmbeddingDeltaPublisher.PublishNodesUpsertAsync(_messagePublisher, _logger, graph.Id, nodeIds);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取图谱节点列表失败，跳过全量重嵌. KgId={KgId}", graph.Id);
        }

        return EmptyCommandResponse.Default;
    }
}
