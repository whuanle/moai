using System;
using System.Collections.Generic;
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
using MoAI.Infra.Exceptions;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Services;

/// <summary>
/// 知识库检索实现：按知识库分别用其 embedding 模型生成查询向量，召回后可选重排序（失败降级）、附带相邻上下文片段.
/// </summary>
[InjectOnScoped]
public class WikiSearchService : IWikiSearchService
{
    private const int MaxChunkQueryCount = 10;

    private readonly DatabaseContext _databaseContext;
    private readonly IEmbeddingGeneratorProvider _embeddingGeneratorProvider;
    private readonly IRerankClient _rerankClient;
    private readonly IWikiEmbeddingVectorStore _vectorStore;
    private readonly ILogger<WikiSearchService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiSearchService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="embeddingGeneratorProvider">向量生成器提供者.</param>
    /// <param name="rerankClient">重排序客户端.</param>
    /// <param name="vectorStore">知识库向量存储.</param>
    /// <param name="logger">日志.</param>
    public WikiSearchService(
        DatabaseContext databaseContext,
        IEmbeddingGeneratorProvider embeddingGeneratorProvider,
        IRerankClient rerankClient,
        IWikiEmbeddingVectorStore vectorStore,
        ILogger<WikiSearchService> logger)
    {
        _databaseContext = databaseContext;
        _embeddingGeneratorProvider = embeddingGeneratorProvider;
        _rerankClient = rerankClient;
        _vectorStore = vectorStore;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WikiSearchHit>> SearchAsync(IReadOnlyCollection<long> wikiIds, string query, int top, CancellationToken cancellationToken = default)
    {
        if (wikiIds == null || wikiIds.Count == 0 || string.IsNullOrWhiteSpace(query) || top <= 0)
        {
            return [];
        }

        var ids = wikiIds.Where(x => x > 0).Distinct().Select(x => (int)x).ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var wikis = await _databaseContext.Wikis
            .Where(x => ids.Contains(x.Id) && x.IsDeleted == 0)
            .Select(x => new { x.Id, x.TeamId, x.EmbeddingModelId, x.EmbeddingDimensions, x.RerankModelId })
            .ToListAsync(cancellationToken);

        var hits = new List<WikiSearchHit>();
        foreach (var wiki in wikis)
        {
            if (wiki.EmbeddingModelId == Guid.Empty || wiki.EmbeddingDimensions <= 0)
            {
                continue;
            }

            var pair = await ResolveModelAsync(wiki.EmbeddingModelId, wiki.TeamId, cancellationToken);
            if (pair == null)
            {
                continue;
            }

            var generator = await _embeddingGeneratorProvider.GetEmbeddingGeneratorAsync(pair.Value.Model, pair.Value.Channel, cancellationToken);
            var embeddings = await generator.GenerateAsync(new[] { query }, null, cancellationToken);
            var vector = embeddings.FirstOrDefault()?.Vector;
            if (vector is null || vector.Value.IsEmpty)
            {
                continue;
            }

            var results = await _vectorStore.SearchAsync(wiki.Id, vector.Value, top, null, cancellationToken);
            var wikiHits = results
                .Select(x => new WikiSearchHit
                {
                    WikiId = x.Record.WikiId,
                    DocumentId = x.Record.DocumentId,
                    ChunkId = x.Record.ChunkId,
                    MetadataType = x.Record.MetadataType,
                    Content = x.Record.Content,
                    Score = x.Score,
                })
                .ToList();

            // 该知识库配置了重排序模型则先重排，再参与多库归并
            await TryRerankAsync(wiki.TeamId, wiki.RerankModelId, query, wikiHits, cancellationToken);
            hits.AddRange(wikiHits);
        }

        if (hits.Count == 0)
        {
            return hits;
        }

        var merged = hits
            .OrderByDescending(x => x.RerankScore ?? x.Score ?? double.MinValue)
            .Take(top * ids.Count)
            .ToList();

        await FillDocumentNamesAsync(merged, cancellationToken);
        await FillChunkInfoAsync(merged, cancellationToken);
        return merged;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WikiSearchHit>> SearchInWikiAsync(int wikiId, string query, int top, double? minScore = null, IReadOnlyCollection<long>? documentIds = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || top <= 0)
        {
            return [];
        }

        var wiki = await _databaseContext.Wikis
            .Where(x => x.Id == wikiId && x.IsDeleted == 0)
            .Select(x => new { x.Id, x.TeamId, x.EmbeddingModelId, x.EmbeddingDimensions, x.RerankModelId })
            .FirstOrDefaultAsync(cancellationToken);
        if (wiki == null)
        {
            throw new BusinessException("知识库不存在.") { StatusCode = 404 };
        }

        if (wiki.EmbeddingModelId == Guid.Empty || wiki.EmbeddingDimensions <= 0)
        {
            throw new BusinessException("知识库未配置向量化模型，无法执行召回.") { StatusCode = 409 };
        }

        var pair = await ResolveModelAsync(wiki.EmbeddingModelId, wiki.TeamId, cancellationToken);
        if (pair == null)
        {
            throw new BusinessException("向量化模型未授权给该团队或未启用.") { StatusCode = 409 };
        }

        var generator = await _embeddingGeneratorProvider.GetEmbeddingGeneratorAsync(pair.Value.Model, pair.Value.Channel, cancellationToken);
        var embeddings = await generator.GenerateAsync(
            [query],
            options: new EmbeddingGenerationOptions { Dimensions = wiki.EmbeddingDimensions },
            cancellationToken: cancellationToken);
        var vector = embeddings.FirstOrDefault()?.Vector;
        if (vector is null || vector.Value.IsEmpty)
        {
            throw new BusinessException("向量模型未返回查询向量.") { StatusCode = 502 };
        }

        var docFilter = documentIds?
            .Where(x => x > 0)
            .Select(x => (int)x)
            .Distinct()
            .ToList();
        var results = await _vectorStore.SearchAsync(wiki.Id, vector.Value, top, docFilter, cancellationToken);

        var hits = results
            .Select(x => new WikiSearchHit
            {
                WikiId = x.Record.WikiId,
                DocumentId = x.Record.DocumentId,
                ChunkId = x.Record.ChunkId,
                MetadataType = x.Record.MetadataType,
                Content = x.Record.Content,
                Score = x.Score,
            })
            .Where(x => !minScore.HasValue || (x.Score ?? double.MinValue) >= minScore.Value)
            .OrderByDescending(x => x.Score ?? double.MinValue)
            .ToList();

        await TryRerankAsync(wiki.TeamId, wiki.RerankModelId, query, hits, cancellationToken);
        await FillDocumentNamesAsync(hits, cancellationToken);
        await FillChunkInfoAsync(hits, cancellationToken);
        return hits;
    }

    /// <inheritdoc/>
    public async Task<WikiDocumentChunkQueryResult> GetDocumentChunksAsync(IReadOnlyCollection<long> wikiIds, int documentId, IReadOnlyCollection<int> chunkIndexes, CancellationToken cancellationToken = default)
    {
        if (wikiIds == null || wikiIds.Count == 0 || documentId <= 0 || chunkIndexes == null || chunkIndexes.Count == 0)
        {
            throw new BusinessException("请提供知识库、文档 id 与切片序号.") { StatusCode = 400 };
        }

        if (chunkIndexes.Count > MaxChunkQueryCount)
        {
            throw new BusinessException($"单次最多获取 {MaxChunkQueryCount} 个切片.") { StatusCode = 400 };
        }

        var wikiIdList = wikiIds.Where(x => x > 0).Distinct().Select(x => (int)x).ToList();
        var indexes = chunkIndexes.Where(x => x >= 0).Distinct().OrderBy(x => x).ToList();
        if (wikiIdList.Count == 0 || indexes.Count == 0)
        {
            throw new BusinessException("请提供有效的知识库、文档 id 与切片序号.") { StatusCode = 400 };
        }

        var document = await _databaseContext.WikiDocuments
            .Where(x => x.Id == documentId && wikiIdList.Contains(x.WikiId) && x.IsDeleted == 0)
            .Select(x => new { x.Id, x.FileName })
            .FirstOrDefaultAsync(cancellationToken);
        if (document == null)
        {
            throw new BusinessException("文档不存在或不属于绑定的知识库.") { StatusCode = 404 };
        }

        var chunks = await _databaseContext.WikiDocumentChunkContents
            .Where(x => x.DocumentId == documentId && x.IsDeleted == 0 && indexes.Contains(x.SliceOrder))
            .OrderBy(x => x.SliceOrder)
            .Select(x => new WikiChunkContent { ChunkId = x.Id, ChunkIndex = x.SliceOrder, Content = x.SliceContent })
            .ToListAsync(cancellationToken);

        return new WikiDocumentChunkQueryResult
        {
            DocumentId = document.Id,
            DocumentName = document.FileName,
            Chunks = chunks,
        };
    }

    /// <summary>
    /// 调用知识库配置的重排序模型对命中项重新打分排序；模型不可用或渠道异常时忽略重排序，保持向量召回顺序.
    /// </summary>
    private async Task TryRerankAsync(int teamId, Guid? rerankModelId, string query, List<WikiSearchHit> hits, CancellationToken cancellationToken)
    {
        if (rerankModelId is null || rerankModelId.Value == Guid.Empty || hits.Count == 0)
        {
            return;
        }

        try
        {
            var pair = await ResolveModelAsync(rerankModelId.Value, teamId, cancellationToken);
            if (pair == null)
            {
                _logger.LogWarning("知识库重排序模型未授权给该团队或未启用，跳过重排序.{ModelId}", rerankModelId);
                return;
            }

            var documents = hits.Select(x => x.Content).ToList();
            var results = await _rerankClient.RerankAsync(pair.Value.Model, pair.Value.Channel, query, documents, cancellationToken: cancellationToken);
            if (results.Count == 0)
            {
                return;
            }

            var scoreMap = new Dictionary<int, double>();
            foreach (var item in results)
            {
                if (item.Index >= 0 && item.Index < hits.Count)
                {
                    scoreMap[item.Index] = item.RelevanceScore;
                }
            }

            var scored = new List<WikiSearchHit>();
            var unscored = new List<WikiSearchHit>();
            for (var i = 0; i < hits.Count; i++)
            {
                if (scoreMap.TryGetValue(i, out var score))
                {
                    hits[i].RerankScore = score;
                    scored.Add(hits[i]);
                }
                else
                {
                    unscored.Add(hits[i]);
                }
            }

            hits.Clear();
            hits.AddRange(scored.OrderByDescending(x => x.RerankScore).Concat(unscored));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // 重排序模型渠道异常不阻断检索整体流程，降级为向量召回顺序
            _logger.LogWarning(ex, "知识库重排序失败，忽略重排序结果.{ModelId}", rerankModelId);
        }
    }

    /// <summary>
    /// 回填命中项的切片序号、相邻上下文片段（前一片段与后一片段，跨命中项按切片去重）与文档切片总数.
    /// </summary>
    private async Task FillChunkInfoAsync(List<WikiSearchHit> hits, CancellationToken cancellationToken)
    {
        if (hits.Count == 0)
        {
            return;
        }

        var chunkIds = hits.Select(x => x.ChunkId).Where(x => x > 0).Distinct().ToList();
        if (chunkIds.Count == 0)
        {
            return;
        }

        var chunkInfos = await _databaseContext.WikiDocumentChunkContents
            .Where(x => chunkIds.Contains(x.Id) && x.IsDeleted == 0)
            .Select(x => new { x.Id, x.DocumentId, x.SliceOrder })
            .ToListAsync(cancellationToken);
        var chunkMap = chunkInfos.ToDictionary(x => x.Id);
        if (chunkMap.Count == 0)
        {
            return;
        }

        // 涉及文档的全部切片序号表：用于定位邻居与统计文档切片总数
        var documentIds = chunkMap.Values.Select(x => x.DocumentId).Distinct().ToList();
        var documentChunks = await _databaseContext.WikiDocumentChunkContents
            .Where(x => documentIds.Contains(x.DocumentId) && x.IsDeleted == 0)
            .Select(x => new { x.Id, x.DocumentId, x.SliceOrder })
            .ToListAsync(cancellationToken);
        var orderMap = documentChunks.ToDictionary(x => (x.DocumentId, x.SliceOrder), x => x.Id);
        var countMap = documentChunks
            .GroupBy(x => x.DocumentId)
            .ToDictionary(g => g.Key, g => g.Count());

        var neighborIds = new HashSet<long>();
        foreach (var hit in hits)
        {
            if (!chunkMap.TryGetValue(hit.ChunkId, out var info))
            {
                continue;
            }

            hit.ChunkIndex = info.SliceOrder;
            if (countMap.TryGetValue(info.DocumentId, out var count))
            {
                hit.DocumentChunkCount = count;
            }

            foreach (var order in NeighborOrders(info.SliceOrder))
            {
                if (orderMap.TryGetValue((info.DocumentId, order), out var neighborId) && neighborId != hit.ChunkId)
                {
                    neighborIds.Add(neighborId);
                }
            }
        }

        var neighborContents = new Dictionary<long, string>();
        if (neighborIds.Count > 0)
        {
            neighborContents = await _databaseContext.WikiDocumentChunkContents
                .Where(x => neighborIds.Contains(x.Id) && x.IsDeleted == 0)
                .Select(x => new { x.Id, x.SliceContent })
                .ToDictionaryAsync(x => x.Id, x => x.SliceContent, cancellationToken);
        }

        // 去重：主命中片段优先，任一片段（含作为其它命中的邻居）只完整输出一次
        var seen = new HashSet<long>(hits.Select(x => x.ChunkId));
        foreach (var hit in hits)
        {
            if (!chunkMap.TryGetValue(hit.ChunkId, out var info))
            {
                continue;
            }

            foreach (var order in NeighborOrders(info.SliceOrder))
            {
                if (!orderMap.TryGetValue((info.DocumentId, order), out var neighborId) ||
                    neighborId == hit.ChunkId ||
                    !seen.Add(neighborId) ||
                    !neighborContents.TryGetValue(neighborId, out var text))
                {
                    continue;
                }

                hit.Context.Add(new WikiSearchContextChunk
                {
                    ChunkId = neighborId,
                    ChunkIndex = order,
                    Content = text,
                });
            }
        }
    }

    private static IEnumerable<int> NeighborOrders(int sliceOrder)
    {
        if (sliceOrder > 0)
        {
            yield return sliceOrder - 1;
        }

        yield return sliceOrder + 1;
    }

    private async Task FillDocumentNamesAsync(List<WikiSearchHit> hits, CancellationToken cancellationToken)
    {
        if (hits.Count == 0)
        {
            return;
        }

        var documentIds = hits.Select(x => x.DocumentId).Distinct().ToList();
        var documentNames = await _databaseContext.WikiDocuments
            .Where(x => documentIds.Contains(x.Id) && x.IsDeleted == 0)
            .Select(x => new { x.Id, x.FileName })
            .ToListAsync(cancellationToken);
        var nameMap = documentNames.ToDictionary(x => x.Id, x => x.FileName);
        foreach (var hit in hits)
        {
            if (nameMap.TryGetValue(hit.DocumentId, out var name))
            {
                hit.DocumentName = name;
            }
        }
    }

    private async Task<(AiModelEntity Model, AiChannelEntity Channel)?> ResolveModelAsync(Guid modelId, int teamId, CancellationToken cancellationToken)
    {
        var candidates = await (from m in _databaseContext.AiModels
                                join c in _databaseContext.AiChannels on m.ChannelId equals c.Id
                                where m.Id == modelId && m.Enabled && c.Enabled
                                select new { m, c })
            .ToListAsync(cancellationToken);

        var model = candidates.FirstOrDefault(x => x.m.IsPublic);
        if (model == null)
        {
            var authorized = await _databaseContext.AiModelAuthorizations
                .AnyAsync(x => x.AiModelId == modelId && x.TeamId == teamId, cancellationToken);
            if (!authorized)
            {
                return null;
            }

            model = candidates.FirstOrDefault();
        }

        return model == null ? null : (model.m, model.c);
    }
}
