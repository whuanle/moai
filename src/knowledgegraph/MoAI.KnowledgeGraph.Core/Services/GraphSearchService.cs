using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using MoAI.AIChannel.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.KnowledgeGraph.Models;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 知识图谱向量检索实现：按图谱分别用其 embedding 模型生成查询向量，召回 top 实体后做一跳关系扩展，最后子图文本化（GraphRAG local search 轻量版）.
/// </summary>
[InjectOnScoped]
public class GraphSearchService : IGraphSearchService
{
    private const int NeighborLimit = 10;
    private const int MaxTextLength = 8192;

    private readonly DatabaseContext _databaseContext;
    private readonly IEmbeddingGeneratorProvider _embeddingGeneratorProvider;
    private readonly IKgEmbeddingVectorStore _vectorStore;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphSearchService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="embeddingGeneratorProvider">向量生成器提供者.</param>
    /// <param name="vectorStore">知识图谱向量存储.</param>
    /// <param name="store">图存储.</param>
    public GraphSearchService(DatabaseContext databaseContext, IEmbeddingGeneratorProvider embeddingGeneratorProvider, IKgEmbeddingVectorStore vectorStore, IKnowledgeGraphStore store)
    {
        _databaseContext = databaseContext;
        _embeddingGeneratorProvider = embeddingGeneratorProvider;
        _vectorStore = vectorStore;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<GraphSearchResult> SearchAsync(IReadOnlyCollection<long> graphIds, string query, int topPerGraph = 5, double? minScore = null, CancellationToken cancellationToken = default)
    {
        if (graphIds == null || graphIds.Count == 0 || string.IsNullOrWhiteSpace(query) || topPerGraph <= 0)
        {
            return new GraphSearchResult([], [], string.Empty, []);
        }

        var ids = graphIds.Where(x => x > 0).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new GraphSearchResult([], [], string.Empty, []);
        }

        var graphs = await _databaseContext.KnowledgeGraphs
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.TeamId, x.Mode, x.EmbeddingModelId, x.EmbeddingDimensions })
            .ToListAsync(cancellationToken);

        var skippedHints = new List<string>();
        var hits = new List<GraphSearchHit>();
        var searchedGraphCount = 0;
        foreach (var graph in graphs)
        {
            if (!string.Equals(graph.Mode, KnowledgeGraphModes.Managed, StringComparison.Ordinal))
            {
                skippedHints.Add($"图谱 {graph.Id} 为接入图谱，不支持向量检索");
                continue;
            }

            // Guid? 判空用 == null，禁止 == Guid.Empty 哨兵（对齐 KgEmbeddingService）
            if (graph.EmbeddingModelId == null || graph.EmbeddingDimensions <= 0)
            {
                skippedHints.Add($"图谱 {graph.Id} 未配置向量化模型，请在图谱设置中配置");
                continue;
            }

            var pair = await ResolveModelAsync(graph.EmbeddingModelId.Value, graph.TeamId, cancellationToken);
            if (pair == null)
            {
                skippedHints.Add($"图谱 {graph.Id} 的向量化模型不可用或未授权");
                continue;
            }

            var generator = await _embeddingGeneratorProvider.GetEmbeddingGeneratorAsync(pair.Value.Model, pair.Value.Channel, cancellationToken);
            var embeddings = await generator.GenerateAsync(
                [query],
                options: new EmbeddingGenerationOptions { Dimensions = graph.EmbeddingDimensions },
                cancellationToken: cancellationToken);
            var vector = embeddings.FirstOrDefault()?.Vector;
            if (vector is null || vector.Value.IsEmpty)
            {
                skippedHints.Add($"图谱 {graph.Id} 查询向量生成失败");
                continue;
            }

            searchedGraphCount++;
            var results = await _vectorStore.SearchAsync(graph.Id, vector.Value, topPerGraph, cancellationToken);
            if (results.Count == 0)
            {
                continue;
            }

            // 类型名按图谱一次性批量建字典，该图多个命中复用
            var entityTypeNames = await _databaseContext.KnowledgeGraphEntityTypes
                .AsNoTracking()
                .Where(x => x.KnowledgeGraphId == graph.Id)
                .Select(x => new { x.Id, x.Name })
                .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
            var relationTypeNames = await _databaseContext.KnowledgeGraphRelationTypes
                .AsNoTracking()
                .Where(x => x.KnowledgeGraphId == graph.Id)
                .Select(x => new { x.Id, x.Name })
                .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

            foreach (var result in results)
            {
                // Score 为 null：无阈值时保留，有阈值时视为不满足（对齐 WikiSearchService.SearchInWikiAsync）
                if (minScore.HasValue && (result.Score ?? double.MinValue) < minScore.Value)
                {
                    continue;
                }

                hits.Add(await BuildHitAsync(graph.Id, result, entityTypeNames, relationTypeNames, cancellationToken));
            }
        }

        if (hits.Count == 0)
        {
            return new GraphSearchResult([], [], string.Empty, skippedHints);
        }

        var ordered = hits
            .OrderByDescending(x => x.Score ?? double.MinValue)
            .Take(topPerGraph * searchedGraphCount)
            .ToList();

        return new GraphSearchResult(ordered, BuildContents(ordered), BuildText(ordered), skippedHints);
    }

    /// <summary>
    /// 组装单个命中：一跳邻居按边补方向与关系类型名，实体类型名由 PG 类型表补齐.
    /// </summary>
    private async Task<GraphSearchHit> BuildHitAsync(
        long kgId,
        KgEmbeddingSearchResult result,
        Dictionary<long, string> entityTypeNames,
        Dictionary<long, string> relationTypeNames,
        CancellationToken cancellationToken)
    {
        var record = result.Record;
        // 第三返回值 Truncated 被丢弃：邻居超 10 有意截断，不做文本提示（LLM 上下文成本考量）
        var (neighborNodes, edges, _) = await _store.GetNeighborsAsync(kgId, record.NodeId, NeighborLimit, cancellationToken);

        // 邻居节点按 id 建字典（同 id 多次返回取首个）
        var nodeMap = new Dictionary<string, KnowledgeGraphNodeRecord>();
        foreach (var node in neighborNodes)
        {
            nodeMap.TryAdd(node.Id, node);
        }

        var neighbors = new List<GraphNeighbor>();
        foreach (var edge in edges)
        {
            var isOut = string.Equals(edge.SourceNodeId, record.NodeId, StringComparison.Ordinal);
            var isIn = string.Equals(edge.TargetNodeId, record.NodeId, StringComparison.Ordinal);
            if (!isOut && !isIn)
            {
                // 不与命中节点相连的边（理论上不会出现）直接跳过
                continue;
            }

            var neighborId = isOut ? edge.TargetNodeId : edge.SourceNodeId;
            if (!nodeMap.TryGetValue(neighborId, out var neighborNode))
            {
                continue;
            }

            relationTypeNames.TryGetValue(edge.RelationTypeId, out var relationName);
            neighbors.Add(new GraphNeighbor(relationName, isOut ? "out" : "in", neighborNode.Name, neighborNode.Description));
        }

        entityTypeNames.TryGetValue(record.EntityTypeId, out var entityTypeName);
        return new GraphSearchHit(kgId, record.NodeId, record.Name, ExtractDescription(record), record.EntityTypeId, entityTypeName, result.Score, neighbors);
    }

    /// <summary>
    /// 从向量化文本还原节点描述：向量化契约（KgEmbeddingService）为「名称\n描述」，契约不符时回退为整段文本.
    /// </summary>
    private static string ExtractDescription(KgEmbeddingVectorRecord record)
    {
        var content = record.Content;
        if (content.Length >= record.Name.Length + 1
            && content.StartsWith(record.Name, StringComparison.Ordinal)
            && content[record.Name.Length] == '\n')
        {
            return content[(record.Name.Length + 1)..];
        }

        return content;
    }

    private static List<string> BuildContents(IReadOnlyList<GraphSearchHit> hits)
        => hits.Select(x => $"{x.Name}：{x.Description}").ToList();

    /// <summary>
    /// 子图文本化：每命中实体一段（首行节点 + 邻居行），段间空行，总长超限截断.
    /// </summary>
    private static string BuildText(IReadOnlyList<GraphSearchHit> hits)
    {
        var builder = new StringBuilder();
        foreach (var hit in hits)
        {
            builder.Append('【').Append(hit.Name).Append('（').Append(hit.EntityTypeName ?? "未知类型").Append("）】").AppendLine(hit.Description);
            foreach (var neighbor in hit.Neighbors)
            {
                builder.Append("  └─ ")
                    .Append(neighbor.RelationName ?? "关联")
                    .Append('(').Append(neighbor.Direction).Append(")→ ")
                    .Append(neighbor.Name).Append('：').AppendLine(neighbor.Description);
            }

            builder.AppendLine();
        }

        var text = builder.ToString().TrimEnd();
        if (text.Length > MaxTextLength)
        {
            text = text[..MaxTextLength] + "…(已截断)";
        }

        return text;
    }

    /// <summary>
    /// 解析团队可用的向量化模型；与 WikiEmbeddingService/KgEmbeddingService 保持语义同步.
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
