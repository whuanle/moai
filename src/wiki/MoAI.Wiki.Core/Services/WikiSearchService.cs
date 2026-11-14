using System.Globalization;
using System.Linq.Expressions;
using Maomi;
using Maomi.MQ;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MoAI.AIChannel.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Statistics.Services;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Services;

/// <summary>
/// 知识库检索实现：按知识库分别用其 embedding 模型生成查询向量，向量召回与关键词召回
/// （切片原文 + 关键词/问题元数据，pg_trgm 索引加速 ILIKE）按 ChunkId 去重融合；
/// 配置了重排序模型时以重排分为最终排序依据，否则以 RRF（Reciprocal Rank Fusion）融合排序兜底；
/// 可选附带相邻上下文片段；回填时剔除已删除文档/切片的命中（存量孤儿向量兜底）并标记内容待更新（IsStale）.
/// </summary>
[InjectOnScoped]
public class WikiSearchService : IWikiSearchService
{
    private const int MaxChunkQueryCount = 10;

    /// <summary>
    /// 关键词检索探针上限（CJK 二元组 + 词元，防 OR 链过长）.
    /// </summary>
    private const int MaxKeywordProbes = 16;

    /// <summary>
    /// 关键词候选抓取上限（切片原文路径与元数据路径各一批）.
    /// </summary>
    private const int KeywordCandidateFetchLimit = 200;

    /// <summary>
    /// 融合候选上限（同时兼容重排序客户端单次 50 文档上限）.
    /// </summary>
    private const int MaxFusedCandidates = 50;

    /// <summary>
    /// 相邻上下文窗口上限（每个命中片段向后/向前扩展的片段数）.
    /// </summary>
    private const int MaxContextWindow = 3;

    private readonly DatabaseContext _databaseContext;
    private readonly IEmbeddingGeneratorProvider _embeddingGeneratorProvider;
    private readonly IRerankClient _rerankClient;
    private readonly IWikiEmbeddingVectorStore _vectorStore;
    private readonly IResourceUsageCollector _usageCollector;
    private readonly WikiRecallClusterService _recallClusterService;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<WikiSearchService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiSearchService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="embeddingGeneratorProvider">向量生成器提供者.</param>
    /// <param name="rerankClient">重排序客户端.</param>
    /// <param name="vectorStore">知识库向量存储.</param>
    /// <param name="usageCollector">轮次使用量收集器（一轮对话内多次检索聚合，作用域结束批量推送）.</param>
    /// <param name="recallClusterService">检索经验聚类服务（沉淀与复用）.</param>
    /// <param name="messagePublisher">消息发布器（聚类沉淀异步落库）.</param>
    /// <param name="logger">日志.</param>
    public WikiSearchService(
        DatabaseContext databaseContext,
        IEmbeddingGeneratorProvider embeddingGeneratorProvider,
        IRerankClient rerankClient,
        IWikiEmbeddingVectorStore vectorStore,
        IResourceUsageCollector usageCollector,
        WikiRecallClusterService recallClusterService,
        IMessagePublisher messagePublisher,
        ILogger<WikiSearchService> logger)
    {
        _databaseContext = databaseContext;
        _embeddingGeneratorProvider = embeddingGeneratorProvider;
        _rerankClient = rerankClient;
        _vectorStore = vectorStore;
        _usageCollector = usageCollector;
        _recallClusterService = recallClusterService;
        _messagePublisher = messagePublisher;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WikiSearchHit>> SearchAsync(IReadOnlyCollection<long> wikiIds, string query, int top, bool isKeywordSearch = true, int contextWindow = 1, CancellationToken cancellationToken = default)
    {
        if (wikiIds == null || wikiIds.Count == 0 || string.IsNullOrWhiteSpace(query) || top <= 0)
        {
            return [];
        }

        contextWindow = Math.Clamp(contextWindow, 1, MaxContextWindow);

        var ids = wikiIds.Where(x => x > 0).Distinct().Select(x => (int)x).ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        // 知识库检索即使用：轮次收集器聚合（一次检索多个知识库各计一次），作用域结束批量推送
        foreach (var id in ids)
        {
            try
            {
                _usageCollector.Record(ResourceUsageNames.Wiki, id.ToString(CultureInfo.InvariantCulture));
            }
#pragma warning disable CA1031 // 使用统计失败不应阻塞检索
            catch (Exception)
            {
                // 使用统计失败不阻塞检索
            }
#pragma warning restore CA1031
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

            // 检索经验复用快速路径：命中聚类则跳过 embedding 调用（文档名/切片序号/上下文由末尾统一回填，此处不预填避免重复）
            if (isKeywordSearch)
            {
            var reused = await _recallClusterService.TryReuseAsync(wiki.Id, query, top, cancellationToken);
            if (reused is { Count: > 0 })
            {
                await _recallClusterService.RecordReuseAsync(wiki.Id, query, cancellationToken);
                hits.AddRange(reused);
                continue;
            }

            }

            var generator = await _embeddingGeneratorProvider.GetEmbeddingGeneratorAsync(pair.Value.Model, pair.Value.Channel, cancellationToken);
            var embeddings = await generator.GenerateAsync(new[] { query }, null, cancellationToken);
            var vector = embeddings.FirstOrDefault()?.Vector;
            if (vector is null || vector.Value.IsEmpty)
            {
                continue;
            }

            var wikiHits = await SearchSingleWikiAsync(
                wiki.Id, wiki.TeamId, wiki.RerankModelId, query, vector.Value,
                top, minScore: null, documentIds: null, isKeywordSearch, cancellationToken);
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

        await ApplyDocumentInfoAsync(merged, cancellationToken);
        await FillChunkInfoAsync(merged, contextWindow, cancellationToken);

        // 剔除切片已不存在（历史孤儿向量、已删除文档/切片）的命中；内容级兜底命中无切片（ChunkId=0）保留
        return merged.Where(x => x.ChunkIndex.HasValue || x.ChunkId == 0).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WikiSearchHit>> SearchInWikiAsync(int wikiId, string query, int top, double? minScore = null, IReadOnlyCollection<long>? documentIds = null, bool isKeywordSearch = true, int contextWindow = 1, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || top <= 0)
        {
            return [];
        }

        contextWindow = Math.Clamp(contextWindow, 1, MaxContextWindow);

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

        var docFilter = documentIds?
            .Where(x => x > 0)
            .Select(x => (int)x)
            .Distinct()
            .ToList();

        // 检索经验复用快速路径：命中聚类则跳过 embedding 调用；
        // 严格阈值模式（minScore>0 表示只要向量相似度达标）与文档范围过滤查询不复用（防止聚类欠服务）
        if (isKeywordSearch && (!minScore.HasValue || minScore.Value <= 0) && (docFilter == null || docFilter.Count == 0))
        {
            var reused = await _recallClusterService.TryReuseAsync(wiki.Id, query, top, cancellationToken);
            if (reused is { Count: > 0 })
            {
                await _recallClusterService.RecordReuseAsync(wiki.Id, query, cancellationToken);
                await ApplyDocumentInfoAsync(reused, cancellationToken);
                await FillChunkInfoAsync(reused, contextWindow, cancellationToken);
                return reused.Where(x => x.ChunkIndex.HasValue).ToList();
            }
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

        var hits = await SearchSingleWikiAsync(
            wiki.Id, wiki.TeamId, wiki.RerankModelId, query, vector.Value,
            top, minScore, docFilter, isKeywordSearch, cancellationToken);

        await ApplyDocumentInfoAsync(hits, cancellationToken);
        await FillChunkInfoAsync(hits, contextWindow, cancellationToken);

        // 剔除切片已不存在（历史孤儿向量、已删除文档/切片）的命中；内容级兜底命中无切片（ChunkId=0）保留
        return hits.Where(x => x.ChunkIndex.HasValue || x.ChunkId == 0).ToList();
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
    /// 单知识库检索核心：向量召回（按 ChunkId 去重、可选相似度阈值）∪ 关键词召回，
    /// 按 ChunkId 合并去重（向量精确度排序在前，关键词补充命中追加在后）；
    /// 配置了重排序模型时对合并候选池整体重排（重排分为最终排序依据），最终返回不超过 top 条.
    /// </summary>
    private async Task<List<WikiSearchHit>> SearchSingleWikiAsync(
        int wikiId,
        int teamId,
        Guid? rerankModelId,
        string query,
        ReadOnlyMemory<float> queryVector,
        int top,
        double? minScore,
        IReadOnlyCollection<int>? documentIds,
        bool isKeywordSearch,
        CancellationToken cancellationToken)
    {
        var results = await _vectorStore.SearchAsync(wikiId, queryVector, top, documentIds, cancellationToken);
        var vectorHits = results
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

        // 同一 chunk 的多条 MetadataType 变体（原文/大纲/问题/关键词/摘要/聚合段）只保留得分最高者，避免占满 top-k
        vectorHits = DedupeByChunk(vectorHits);
        vectorHits = vectorHits
            .Where(x => !minScore.HasValue || (x.Score ?? double.MinValue) >= minScore.Value)
            .OrderByDescending(x => x.Score ?? double.MinValue)
            .ToList();

        // 显式设置相似度阈值（>0）表示只要向量相似度达标的命中；关键词命中不带相似度分，此时退化为纯向量召回

        var keywordHits = isKeywordSearch && (!minScore.HasValue || minScore.Value <= 0)
            ? await KeywordSearchAsync(wikiId, query, top, documentIds, cancellationToken)
            : [];

        // 内容级兜底：chunk 候选为空（文档已提取未切割）时探提取正文返回文档级片段命中（上传提取后即可查）
        if (keywordHits.Count == 0 && isKeywordSearch && (!minScore.HasValue || minScore.Value <= 0))
        {
            keywordHits = await ContentFallbackSearchAsync(wikiId, query, top, documentIds, cancellationToken);
        }

        var merged = FuseHits(vectorHits, keywordHits);
        if (merged.Count > MaxFusedCandidates)
        {
            merged = merged.Take(MaxFusedCandidates).ToList();
        }

        // 该知识库配置了重排序模型则对融合候选重排，重排分成为最终排序依据；失败降级保持融合顺序
        await TryRerankAsync(teamId, rerankModelId, query, merged, cancellationToken);
        if (merged.Count > top)
        {
            merged = merged.Take(top).ToList();
        }

        // 检索经验沉淀：把验证过的查询→命中异步落库供后续同查询复用（发布失败仅记日志不阻断）；
        // 文档范围过滤的检索结果不沉淀（范围结果对全库查询不可复用，防止欠服务）；
        // 含内容级兜底命中（ChunkId=0）的结果不沉淀（文档切片未定，切割后命中会变化）
        if (merged.Count > 0 && (documentIds is null || documentIds.Count == 0) && merged.All(x => x.ChunkId != 0))
        {
            await WikiRecallClusterPublisher.PublishAsync(_messagePublisher, _logger, wikiId, query, merged);
        }

        return merged;
    }

    /// <summary>
    /// 按切片 id 去重：同一切片的多条命中保留得分最高者，保持输入（向量召回）顺序.
    /// </summary>
    private static List<WikiSearchHit> DedupeByChunk(List<WikiSearchHit> hits)
    {
        if (hits.Count <= 1)
        {
            return hits;
        }

        var best = new Dictionary<long, WikiSearchHit>();
        foreach (var hit in hits)
        {
            if (!best.TryGetValue(hit.ChunkId, out var current) || (hit.Score ?? double.MinValue) > (current.Score ?? double.MinValue))
            {
                best[hit.ChunkId] = hit;
            }
        }

        return hits.Where(x => ReferenceEquals(best.GetValueOrDefault(x.ChunkId), x)).ToList();
    }

    /// <summary>
    /// 融合向量与关键词两路命中：同一切片合并标记 <see cref="WikiSearchHit.IsKeywordHit"/>；
    /// 向量命中保持精确度排序在前，关键词补充命中按命中探针数追加在后（向量精确度优先，关键词提升覆盖）.
    /// </summary>
    private static List<WikiSearchHit> FuseHits(List<WikiSearchHit> vectorHits, List<WikiSearchHit> keywordHits)
    {
        var merged = new List<WikiSearchHit>(vectorHits);
        var seen = new HashSet<long>(vectorHits.Select(x => x.ChunkId));
        foreach (var keywordHit in keywordHits)
        {
            if (seen.Add(keywordHit.ChunkId))
            {
                merged.Add(keywordHit);
            }
            else
            {
                merged.First(x => x.ChunkId == keywordHit.ChunkId).IsKeywordHit = true;
            }
        }

        return merged;
    }

    /// <summary>
    /// 关键词召回路径（低成本先验）：查询词生成探针（CJK 二元组 + 词元），ILIKE 匹配切片原文与
    /// 关键词/问题元数据（pg_trgm GIN 索引加速），按命中探针数排序返回候选.
    /// </summary>
    private async Task<List<WikiSearchHit>> KeywordSearchAsync(int wikiId, string query, int top, IReadOnlyCollection<int>? documentIds, CancellationToken cancellationToken)
    {
        var probes = BuildKeywordProbes(query);
        if (probes.Count == 0)
        {
            return [];
        }

        var docFilter = documentIds is { Count: > 0 } ? documentIds.ToList() : null;

        // 切片原文候选
        var contentQuery = _databaseContext.WikiDocumentChunkContents.AsNoTracking()
            .Where(x => x.WikiId == wikiId && x.IsDeleted == 0);
        if (docFilter != null)
        {
            contentQuery = contentQuery.Where(x => docFilter.Contains(x.DocumentId));
        }

        var contentCandidates = await contentQuery
            .Where(BuildAnyLikeExpression<WikiDocumentChunkContentEntity>(x => x.SliceContent, probes))
            .OrderBy(x => x.Id)
            .Take(KeywordCandidateFetchLimit)
            .Select(x => new { x.Id, x.DocumentId, x.SliceContent })
            .ToListAsync(cancellationToken);

        // 关键词（3）/问题（2）元数据候选：入库时已由 LLM 抽取，是现成的低成本先验
        var metadataQuery = _databaseContext.WikiDocumentChunkMetadata.AsNoTracking()
            .Where(x => x.WikiId == wikiId && x.IsDeleted == 0 && (x.MetadataType == 2 || x.MetadataType == 3));
        if (docFilter != null)
        {
            metadataQuery = metadataQuery.Where(x => docFilter.Contains(x.DocumentId));
        }

        var metadataCandidates = await metadataQuery
            .Where(BuildAnyLikeExpression<WikiDocumentChunkMetadatumEntity>(x => x.MetadataContent, probes))
            .OrderBy(x => x.Id)
            .Take(KeywordCandidateFetchLimit)
            .Select(x => new { x.ChunkId })
            .ToListAsync(cancellationToken);

        if (contentCandidates.Count == 0 && metadataCandidates.Count == 0)
        {
            return [];
        }

        // 按命中探针数打分（ILIKE 大小写不敏感，内存比对用 OrdinalIgnoreCase 对齐）
        var scores = new Dictionary<long, int>();
        var documents = new Dictionary<long, int>();
        var contents = new Dictionary<long, string>();
        foreach (var candidate in contentCandidates)
        {
            var hitCount = 0;
            foreach (var probe in probes)
            {
                if (candidate.SliceContent.Contains(probe, StringComparison.OrdinalIgnoreCase))
                {
                    hitCount++;
                }
            }

            if (hitCount <= 0)
            {
                continue;
            }

            scores[candidate.Id] = scores.GetValueOrDefault(candidate.Id) + hitCount;
            documents[candidate.Id] = candidate.DocumentId;
            contents[candidate.Id] = candidate.SliceContent;
        }

        foreach (var candidate in metadataCandidates)
        {
            scores[candidate.ChunkId] = scores.GetValueOrDefault(candidate.ChunkId) + 1;
        }

        var topChunkIds = scores
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key)
            .Take(top * 2)
            .Select(x => x.Key)
            .ToList();
        if (topChunkIds.Count == 0)
        {
            return [];
        }

        // 仅命中元数据的切片需补查切片原文与文档归属
        var missingIds = topChunkIds.Where(x => !contents.ContainsKey(x)).ToList();
        if (missingIds.Count > 0)
        {
            var rows = await _databaseContext.WikiDocumentChunkContents.AsNoTracking()
                .Where(x => missingIds.Contains(x.Id) && x.IsDeleted == 0 && x.WikiId == wikiId)
                .Select(x => new { x.Id, x.DocumentId, x.SliceContent })
                .ToListAsync(cancellationToken);
            foreach (var row in rows)
            {
                contents[row.Id] = row.SliceContent;
                documents[row.Id] = row.DocumentId;
            }
        }

        var hits = new List<WikiSearchHit>();
        foreach (var chunkId in topChunkIds)
        {
            if (!contents.TryGetValue(chunkId, out var content) || !documents.TryGetValue(chunkId, out var documentId))
            {
                continue;
            }

            hits.Add(new WikiSearchHit
            {
                WikiId = wikiId,
                DocumentId = documentId,
                ChunkId = chunkId,
                MetadataType = 0,
                Content = content,
                IsKeywordHit = true,
            });
        }

        return hits;
    }

    /// <summary>
    /// 内容级兜底检索（文档已提取但尚未切割）：探针 ILIKE 匹配提取正文（pg_trgm 索引加速），
    /// 按命中探针数排序返回文档级片段命中——片段取首个探针命中位置前 200/后 700 字符窗口（参数化 raw SQL，
    /// 不整段拉取正文）；命中 ChunkId=0 且无切片序号，切割后由正常切片命中路径接管.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="query">查询文本.</param>
    /// <param name="top">返回条数上限.</param>
    /// <param name="documentIds">文档范围过滤.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>文档级片段命中；探针无法生成或查询失败返回空（兜底路径不阻断检索）.</returns>
    private async Task<List<WikiSearchHit>> ContentFallbackSearchAsync(int wikiId, string query, int top, IReadOnlyCollection<int>? documentIds, CancellationToken cancellationToken)
    {
        var probes = BuildKeywordProbes(query);
        if (probes.Count == 0)
        {
            return [];
        }

        var parameters = new List<NpgsqlParameter>
        {
            new("wikiId", wikiId),
            new("limit", top),
        };
        var ilikeClauses = new List<string>();
        var strposClauses = new List<string>();
        var scoreClauses = new List<string>();
        for (var i = 0; i < probes.Count; i++)
        {
            var patternParam = $"p{i}";
            var strposParam = $"pr{i}";
            parameters.Add(new NpgsqlParameter(patternParam, $"%{probes[i]}%"));
            parameters.Add(new NpgsqlParameter(strposParam, probes[i].ToLowerInvariant()));
            ilikeClauses.Add($"c.content ILIKE @{patternParam}");
            strposClauses.Add($"NULLIF(strpos(lower(c.content), @{strposParam}), 0)");
            scoreClauses.Add($"(CASE WHEN c.content ILIKE @{patternParam} THEN 1 ELSE 0 END)");
        }

        var docFilterSql = string.Empty;
        if (documentIds is { Count: > 0 })
        {
            parameters.Add(new NpgsqlParameter("docIds", documentIds.ToArray()));
            docFilterSql = " AND c.document_id = ANY(@docIds)";
        }

        var sql = $"""
SELECT c.document_id AS "DocumentId",
       substring(c.content FROM GREATEST(1, COALESCE({string.Join(", ", strposClauses)}) - 200) FOR 700) AS "Snippet",
       {string.Join(" + ", scoreClauses)} AS "HitCount"
FROM wiki_document_content AS c
WHERE c.wiki_id = @wikiId AND c.is_deleted = 0{docFilterSql} AND ({string.Join(" OR ", ilikeClauses)})
ORDER BY "HitCount" DESC, c.document_id
LIMIT @limit
""";

        List<ContentFallbackRow> rows;
        try
        {
            rows = await _databaseContext.Database
                .SqlQueryRaw<ContentFallbackRow>(sql, parameters.ToArray())
                .ToListAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // 兜底路径失败不阻断检索整体流程
            _logger.LogWarning(ex, "内容级兜底检索失败，忽略兜底命中. WikiId={WikiId}", wikiId);
            return [];
        }

        return rows
            .Where(x => !string.IsNullOrWhiteSpace(x.Snippet))
            .Select(row => new WikiSearchHit
            {
                WikiId = wikiId,
                DocumentId = row.DocumentId,
                ChunkId = 0,
                MetadataType = 0,
                Content = row.Snippet,
                IsKeywordHit = true,
                IsContentFallback = true,
            })
            .ToList();
    }

    /// <summary>
    /// 内容级兜底检索的行投影（SqlQueryRaw 列别名映射）.
    /// </summary>
    private sealed class ContentFallbackRow
    {
        public int DocumentId { get; set; }

        public string Snippet { get; set; } = string.Empty;

        public int HitCount { get; set; }
    }

    /// <summary>
    /// 生成关键词探针：按空白切词；CJK 连续段滑窗取二元组（无中文分词器时以 bigram 近似词匹配），
    /// 拉丁/数字词元（≥2 字符）保留整词；剔除 LIKE 通配符，全局去重并限制数量.
    /// </summary>
    /// <param name="query">查询文本.</param>
    /// <returns>探针列表（ILIKE 匹配用，已去重限流）.</returns>
    public static IReadOnlyList<string> BuildKeywordProbes(string query)
    {
        // ILIKE 模式通配符按普通字符处理会造成误匹配，直接剔除
        var sanitized = query.Replace('%', ' ').Replace('_', ' ').Replace('\\', ' ');
        var probes = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void AddProbe(string probe)
        {
            if (probes.Count < MaxKeywordProbes && seen.Add(probe))
            {
                probes.Add(probe);
            }
        }

        var tokens = sanitized.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            foreach (var run in SplitRuns(token))
            {
                if (IsCjkRun(run))
                {
                    if (run.Length == 1)
                    {
                        AddProbe(run);
                    }
                    else
                    {
                        for (var i = 0; i + 2 <= run.Length; i++)
                        {
                            AddProbe(run.Substring(i, 2));
                        }
                    }
                }
                else if (run.Length >= 2)
                {
                    // ILIKE 与内存比对均大小写不敏感，词元原样作为探针即可
                    AddProbe(run);
                }
            }

            if (probes.Count >= MaxKeywordProbes)
            {
                break;
            }
        }

        return probes;
    }

    /// <summary>
    /// 将词元切分为 CJK 连续段与拉丁/数字段（标点等其它字符作为分隔）.
    /// </summary>
    private static List<string> SplitRuns(string token)
    {
        var runs = new List<string>();
        var current = new List<char>();
        var currentIsCjk = false;
        foreach (var ch in token)
        {
            var isCjk = IsCjkChar(ch);
            if (!isCjk && !char.IsLetterOrDigit(ch))
            {
                if (current.Count > 0)
                {
                    runs.Add(new string(current.ToArray()));
                    current.Clear();
                }

                currentIsCjk = false;
                continue;
            }

            if (current.Count > 0 && isCjk != currentIsCjk)
            {
                runs.Add(new string(current.ToArray()));
                current.Clear();
            }

            currentIsCjk = isCjk;
            current.Add(ch);
        }

        if (current.Count > 0)
        {
            runs.Add(new string(current.ToArray()));
        }

        return runs;
    }

    private static bool IsCjkRun(string run) => run.Length > 0 && run.All(IsCjkChar);

    /// <summary>
    /// 是否 CJK 字符（覆盖统一表意文字及扩展 A/部首符号区，覆盖中日常用字）.
    /// </summary>
    private static bool IsCjkChar(char ch)
        => ch is >= (char)0x2E80 and <= (char)0x9FFF;

    /// <summary>
    /// 构造「字段 ILIKE 任一探针」的 OR 链表达式（探针按子串匹配包裹 %，参数化翻译为 SQL，pg_trgm 索引可加速）.
    /// </summary>
    private static Expression<Func<TEntity, bool>> BuildAnyLikeExpression<TEntity>(Expression<Func<TEntity, string>> fieldSelector, IReadOnlyList<string> probes)
    {
        var parameter = fieldSelector.Parameters[0];
        // ILIKE 为 Npgsql 提供者扩展（EF.Functions.ILike）；探针不带通配符，包裹 % 表达子串包含语义
        var ilikeMethod = typeof(NpgsqlDbFunctionsExtensions).GetMethod(
            nameof(NpgsqlDbFunctionsExtensions.ILike),
            [typeof(DbFunctions), typeof(string), typeof(string)])!;
        var dbFunctions = Expression.Property(null, typeof(EF).GetProperty(nameof(EF.Functions))!);

        Expression? body = null;
        foreach (var probe in probes)
        {
            var pattern = $"%{probe}%";
            var call = Expression.Call(ilikeMethod, dbFunctions, fieldSelector.Body, Expression.Constant(pattern));
            body = body == null ? call : Expression.OrElse(body, call);
        }

        return Expression.Lambda<Func<TEntity, bool>>(body!, parameter);
    }

    /// <summary>
    /// 调用知识库配置的重排序模型对命中项重新打分排序；模型不可用或渠道异常时忽略重排序，保持融合召回顺序.
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
            // 重排序模型渠道异常不阻断检索整体流程，降级为融合召回顺序
            _logger.LogWarning(ex, "知识库重排序失败，忽略重排序结果.{ModelId}", rerankModelId);
        }
    }

    /// <summary>
    /// 回填命中项的切片序号、相邻上下文片段（命中片段前后各 <paramref name="contextWindow"/> 片段，跨命中项按切片去重）与文档切片总数.
    /// </summary>
    /// <param name="hits">命中列表.</param>
    /// <param name="contextWindow">相邻上下文窗口大小（1-3）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    private async Task FillChunkInfoAsync(List<WikiSearchHit> hits, int contextWindow, CancellationToken cancellationToken)
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
            .Select(x => new { x.Id, x.DocumentId, x.SliceOrder, x.SliceAnchor })
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
            hit.Anchor = info.SliceAnchor ?? string.Empty;
            if (countMap.TryGetValue(info.DocumentId, out var count))
            {
                hit.DocumentChunkCount = count;
            }

            foreach (var order in NeighborOrders(info.SliceOrder, contextWindow))
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

            foreach (var order in NeighborOrders(info.SliceOrder, contextWindow))
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

    private static IEnumerable<int> NeighborOrders(int sliceOrder, int contextWindow)
    {
        for (var offset = 1; offset <= contextWindow; offset++)
        {
            if (sliceOrder - offset >= 0)
            {
                yield return sliceOrder - offset;
            }
        }

        for (var offset = 1; offset <= contextWindow; offset++)
        {
            yield return sliceOrder + offset;
        }
    }

    /// <summary>
    /// 回填命中项的文档名与过期标记：文档已软删的命中直接剔除（存量孤儿向量兜底），
    /// 文档内容已更新且重新向量化未完成（IsUpdate）的命中标记 <see cref="WikiSearchHit.IsStale"/>.
    /// </summary>
    private async Task ApplyDocumentInfoAsync(List<WikiSearchHit> hits, CancellationToken cancellationToken)
    {
        if (hits.Count == 0)
        {
            return;
        }

        var documentIds = hits.Select(x => x.DocumentId).Distinct().ToList();
        var documents = await _databaseContext.WikiDocuments
            .Where(x => documentIds.Contains(x.Id) && x.IsDeleted == 0)
            .Select(x => new { x.Id, x.FileName, x.IsUpdate })
            .ToListAsync(cancellationToken);
        var documentMap = documents.ToDictionary(x => x.Id);

        hits.RemoveAll(x => !documentMap.ContainsKey(x.DocumentId));
        foreach (var hit in hits)
        {
            var document = documentMap[hit.DocumentId];
            hit.DocumentName = document.FileName;
            hit.IsStale = document.IsUpdate;
        }
    }

    private async Task<(AiModelEntity Model, AiChannelEntity Channel)?> ResolveModelAsync(Guid modelId, int teamId, CancellationToken cancellationToken)
    {
        var candidates = await (from m in _databaseContext.AiModels
                                join c in _databaseContext.AiChannels on m.ChannelId equals c.Id
                                where m.Id == modelId && m.Enabled && c.Enabled
                                select new { m, c })
            .ToListAsync(cancellationToken);

        var model = candidates.FirstOrDefault(x => x.c.TeamId == teamId) ?? candidates.FirstOrDefault(x => x.m.IsPublic);
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
