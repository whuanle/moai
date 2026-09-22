using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MoAI.AIChannel.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.KnowledgeGraph.Consumers.Events;
using MoAI.KnowledgeGraph.Models;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 知识图谱节点向量增量处理服务：按 delta 消息重建/删除节点向量（幂等，upsert 读图库最新状态）.
/// </summary>
[InjectOnScoped]
public class KgEmbeddingService : IKgEmbeddingService
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKgEmbeddingVectorStore _vectorStore;
    private readonly IKnowledgeGraphStore _store;
    private readonly IEmbeddingGeneratorProvider _embeddingGeneratorProvider;
    private readonly ILogger<KgEmbeddingService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="KgEmbeddingService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="vectorStore">向量存储.</param>
    /// <param name="store">图存储.</param>
    /// <param name="embeddingGeneratorProvider">向量生成器提供者.</param>
    /// <param name="logger">日志.</param>
    public KgEmbeddingService(DatabaseContext databaseContext, IKgEmbeddingVectorStore vectorStore, IKnowledgeGraphStore store, IEmbeddingGeneratorProvider embeddingGeneratorProvider, ILogger<KgEmbeddingService> logger)
    {
        _databaseContext = databaseContext;
        _vectorStore = vectorStore;
        _store = store;
        _embeddingGeneratorProvider = embeddingGeneratorProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ProcessDeltaAsync(KgNodeEmbeddingDeltaMessage message, CancellationToken cancellationToken)
    {
        if (message.KgId <= 0 || (message.UpsertNodeIds.Count == 0 && message.DeleteNodeIds.Count == 0))
        {
            return;
        }

        var graph = await _databaseContext.KnowledgeGraphs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == message.KgId, cancellationToken);
        if (graph == null || !string.Equals(graph.Mode, KnowledgeGraphModes.Managed, StringComparison.Ordinal))
        {
            return;
        }

        // 未配置向量化（自动触发场景）静默跳过；Guid? 判空用 == null，禁止 == Guid.Empty 哨兵
        if (graph.EmbeddingModelId == null || graph.EmbeddingDimensions <= 0)
        {
            return;
        }

        var pair = await ResolveModelAsync(graph.EmbeddingModelId.Value, graph.TeamId, cancellationToken);
        if (pair == null)
        {
            return;
        }

        var generator = await _embeddingGeneratorProvider.GetEmbeddingGeneratorAsync(pair.Value.Model, pair.Value.Channel, cancellationToken);

        // 删除在前：逐节点独立 try/catch，单点失败记日志继续
        var deleteIds = message.DeleteNodeIds.Distinct().ToList();
        var deleteFailed = 0;
        Exception? lastDeleteError = null;
        foreach (var nodeId in deleteIds)
        {
            try
            {
                await _vectorStore.DeleteNodeVectorsAsync(message.KgId, nodeId, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                deleteFailed++;
                lastDeleteError = ex;
                _logger.LogError(ex, "删除节点向量失败. KgId={KgId}, NodeId={NodeId}", message.KgId, nodeId);
            }
        }

        var upsertIds = message.UpsertNodeIds.Distinct().ToList();
        var upsertFailed = 0;
        Exception? lastUpsertError = null;
        foreach (var nodeId in upsertIds)
        {
            try
            {
                await UpsertNodeAsync(generator, graph, nodeId, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                upsertFailed++;
                lastUpsertError = ex;
                _logger.LogError(ex, "重建节点向量失败. KgId={KgId}, NodeId={NodeId}", message.KgId, nodeId);
            }
        }

        // 全部失败才重投：单点失败仅记日志（delta 幂等，可由后续节点操作或重嵌补齐）
        if (upsertIds.Count > 0 && upsertFailed == upsertIds.Count)
        {
            throw lastUpsertError!;
        }

        if (deleteIds.Count > 0 && deleteFailed == deleteIds.Count)
        {
            throw lastDeleteError!;
        }
    }

    private async Task UpsertNodeAsync(IEmbeddingGenerator<string, Embedding<float>> generator, KnowledgeGraphEntity graph, string nodeId, CancellationToken cancellationToken)
    {
        var node = await _store.GetNodeAsync(graph.Id, nodeId, cancellationToken);
        if (node == null)
        {
            // 节点已不存在（延迟到达的 delta）：视为成功，无向量可写
            return;
        }

        if (string.IsNullOrWhiteSpace(node.Name) && string.IsNullOrWhiteSpace(node.Description))
        {
            await _vectorStore.DeleteNodeVectorsAsync(graph.Id, nodeId, cancellationToken);
            return;
        }

        var content = $"{node.Name}\n{node.Description}";
        var embeddings = await generator.GenerateAsync(
            [content],
            options: new EmbeddingGenerationOptions { Dimensions = graph.EmbeddingDimensions },
            cancellationToken: cancellationToken);

        if (embeddings.Count == 0)
        {
            throw new InvalidOperationException($"向量模型未返回向量：NodeId={nodeId}。");
        }

        var vector = embeddings[0].Vector;
        if (vector.Length == 0)
        {
            throw new InvalidOperationException($"向量模型返回空向量：NodeId={nodeId}。");
        }

        await _vectorStore.ReplaceNodeVectorsAsync(graph.Id, nodeId, [new KgEmbeddingVectorRecord
        {
            Key = Guid.CreateVersion7(),
            KgId = graph.Id,
            NodeId = nodeId,
            EntityTypeId = node.EntityTypeId,
            Name = node.Name,
            Content = content,
            Embedding = vector,
        }], cancellationToken);
    }

    /// <summary>
    /// 解析团队可用的向量化模型；与 WikiEmbeddingService.ResolveModelAsync 保持语义同步.
    /// </summary>
    private async Task<(AiModelEntity Model, AiChannelEntity Channel)?> ResolveModelAsync(Guid modelId, int teamId, CancellationToken cancellationToken)
    {
        var query = from m in _databaseContext.AiModels
                    join c in _databaseContext.AiChannels on m.ChannelId equals c.Id
                    where m.Id == modelId && m.Enabled && c.Enabled && m.IsDeleted == 0 && c.IsDeleted == 0
                    select new { m, c };

        var candidates = await query.ToListAsync(cancellationToken);
        var model = candidates.FirstOrDefault(x => x.m.IsPublic);
        if (model == null)
        {
            var authorized = await _databaseContext.AiModelAuthorizations
                .AnyAsync(x => x.AiModelId == modelId && x.TeamId == teamId, cancellationToken);
            if (authorized)
            {
                model = candidates.FirstOrDefault();
            }
        }

        return model == null ? null : (model.m, model.c);
    }
}
