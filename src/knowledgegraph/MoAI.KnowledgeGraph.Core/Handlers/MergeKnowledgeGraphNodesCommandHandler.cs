using Maomi.MQ;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="MergeKnowledgeGraphNodesCommand"/>
/// </summary>
public class MergeKnowledgeGraphNodesCommandHandler : IRequestHandler<MergeKnowledgeGraphNodesCommand, MergeKnowledgeGraphNodesCommandResponse>
{
    private const int ConfirmThreshold = 20;
    private const int MaxDescriptionLength = 2000;

    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphStore _store;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<MergeKnowledgeGraphNodesCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MergeKnowledgeGraphNodesCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="store">图存储.</param>
    /// <param name="messagePublisher">消息发布器.</param>
    /// <param name="logger">日志.</param>
    public MergeKnowledgeGraphNodesCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphStore store, IMessagePublisher messagePublisher, ILogger<MergeKnowledgeGraphNodesCommandHandler> logger)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _store = store;
        _messagePublisher = messagePublisher;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<MergeKnowledgeGraphNodesCommandResponse> Handle(MergeKnowledgeGraphNodesCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeManagedAsync(request.KnowledgeGraphId, adminOnly: true, cancellationToken);

        var sourceIdSet = request.SourceNodeIds.ToHashSet(StringComparer.Ordinal);
        var target = await _store.GetNodeAsync(request.KnowledgeGraphId, request.TargetNodeId, cancellationToken)
            ?? throw new BusinessException("目标节点不存在.") { StatusCode = 404 };

        // 逐个读取源节点（≤20 上限由校验器保证）
        var sources = new List<KnowledgeGraphNodeRecord>(request.SourceNodeIds.Count);
        foreach (var sourceId in request.SourceNodeIds)
        {
            sources.Add(await _store.GetNodeAsync(request.KnowledgeGraphId, sourceId, cancellationToken)
                ?? throw new BusinessException($"源节点 {sourceId} 不存在.") { StatusCode = 404 });
        }

        var touchingEdges = await _store.GetEdgesTouchingNodesAsync(request.KnowledgeGraphId, request.SourceNodeIds, cancellationToken);

        // 计算重挂边：源端点替换为目标，同三元组去重，自环（含原 source→target 边）丢弃
        var droppedSelfLoops = 0;
        var candidateTriples = new HashSet<(string SourceNodeId, long RelationTypeId, string TargetNodeId)>();
        foreach (var edge in touchingEdges)
        {
            var source = sourceIdSet.Contains(edge.SourceNodeId) ? target.Id : edge.SourceNodeId;
            var target_ = sourceIdSet.Contains(edge.TargetNodeId) ? target.Id : edge.TargetNodeId;
            if (source == target_)
            {
                droppedSelfLoops++;
                continue;
            }

            candidateTriples.Add((source, edge.RelationTypeId, target_));
        }

        // 起止类型约束校验：重挂后目标节点类型必须满足受影响关系类型的起/止类型定义
        var affectedRelationTypeIds = candidateTriples.Select(x => x.RelationTypeId).Distinct().ToList();
        var relationTypes = await _databaseContext.KnowledgeGraphRelationTypes
            .Where(x => x.KnowledgeGraphId == request.KnowledgeGraphId && affectedRelationTypeIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name, x.SourceTypeId, x.TargetTypeId })
            .ToListAsync(cancellationToken);
        var relationTypeById = relationTypes.ToDictionary(x => x.Id);
        foreach (var triple in candidateTriples)
        {
            if (!relationTypeById.TryGetValue(triple.RelationTypeId, out var relationType))
            {
                throw new BusinessException($"关系类型 {triple.RelationTypeId} 不存在.") { StatusCode = 400 };
            }

            var isTargetSource = triple.SourceNodeId == target.Id;
            var requiredType = isTargetSource ? relationType.SourceTypeId : relationType.TargetTypeId;
            if (requiredType.HasValue && requiredType.Value != target.EntityTypeId)
            {
                throw new BusinessException($"合并后关系「{relationType.Name}」的起止类型约束不满足，请先调整目标节点类型或跳过该关系的源节点.") { StatusCode = 400 };
            }
        }

        // 大影响面二段确认
        if (touchingEdges.Count >= ConfirmThreshold && !request.Confirm)
        {
            throw new BusinessException($"本次合并将影响 {touchingEdges.Count} 条关系，请确认后重试.") { StatusCode = 409 };
        }

        // 跳过目标上已存在的同三元组边（幂等重挂）
        var existingKeys = await _store.GetExistingEdgeKeysAsync(
            request.KnowledgeGraphId,
            candidateTriples.Select(x => (x.SourceNodeId, x.RelationTypeId, x.TargetNodeId)).ToList(),
            cancellationToken);
        var edgesToCreate = candidateTriples
            .Where(x => !existingKeys.Contains($"{x.SourceNodeId}|{x.RelationTypeId}|{x.TargetNodeId}"))
            .Select(x => new KnowledgeGraphEdgeInput(x.RelationTypeId, x.SourceNodeId, x.TargetNodeId))
            .ToList();

        // 描述合并：目标描述在前，源描述去重追加，超长截断
        var descriptions = new List<string> { target.Description };
        descriptions.AddRange(sources.Select(x => x.Description));
        var mergedDescription = string.Join(
            "\n",
            descriptions.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal));
        if (mergedDescription.Length > MaxDescriptionLength)
        {
            mergedDescription = mergedDescription[..MaxDescriptionLength];
        }

        // 属性深合并：目标优先，源补充新增键
        var mergedProps = KnowledgeGraphPropertyJson.ParseValues(target.PropsJson);
        foreach (var source in sources)
        {
            foreach (var pair in KnowledgeGraphPropertyJson.ParseValues(source.PropsJson))
            {
                mergedProps.TryAdd(pair.Key, pair.Value);
            }
        }

        var mergedPropsJson = mergedProps.Count == 0 ? null : KnowledgeGraphPropertyJson.WriteValues(mergedProps);

        var rehungEdges = await _store.MergeNodesAsync(
            request.KnowledgeGraphId,
            target.Id,
            mergedDescription,
            mergedPropsJson,
            edgesToCreate,
            request.SourceNodeIds,
            cancellationToken);

        // 向量增量：目标重建（描述/属性已变），源删除
        await KgEmbeddingDeltaPublisher.PublishNodeUpsertAsync(_messagePublisher, _logger, request.KnowledgeGraphId, target.Id);
        await KgEmbeddingDeltaPublisher.PublishNodesDeleteAsync(_messagePublisher, _logger, request.KnowledgeGraphId, request.SourceNodeIds);

        return new MergeKnowledgeGraphNodesCommandResponse
        {
            RehungEdges = rehungEdges,
            DroppedSelfLoopEdges = droppedSelfLoops,
            RemovedNodes = sources.Count,
        };
    }
}
