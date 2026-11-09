using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Services;
using MoAI.Wiki.Consumers.Events;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Services;

/// <summary>
/// 知识库检索经验聚类领域服务：把验证过的查询→命中沉淀为可复用知识单元（Sirchmunk KnowledgeCluster 轻量版）.
/// 复用口径：同 (wikiId, 归一化查询哈希) 命中聚类 → 回源校验切片存活（来源变化即失效自愈）→
/// 存活数 ≥ max(1, 半数) 直接构建命中返回（跳过 embedding 调用），内容/上下文/过期标记由检索服务既有回填链路取最新.
/// </summary>
[InjectOnScoped]
public class WikiRecallClusterService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly DatabaseContext _databaseContext;
    private readonly IIdProvider _idProvider;
    private readonly ILogger<WikiRecallClusterService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiRecallClusterService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="idProvider">雪花 id 提供者.</param>
    /// <param name="logger">日志.</param>
    public WikiRecallClusterService(DatabaseContext databaseContext, IIdProvider idProvider, ILogger<WikiRecallClusterService> logger)
    {
        _databaseContext = databaseContext;
        _idProvider = idProvider;
        _logger = logger;
    }

    /// <summary>
    /// 查询归一化：trim + 内部空白折叠为单空格，超长截断.
    /// </summary>
    /// <param name="query">查询原文.</param>
    /// <returns>归一化查询.</returns>
    public static string NormalizeQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return string.Empty;
        }

        var normalized = string.Join(' ', query.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length <= 1000 ? normalized : normalized[..1000];
    }

    /// <summary>
    /// 计算归一化查询的 SHA-256（小写 hex）.
    /// </summary>
    /// <param name="normalizedQuery">归一化查询.</param>
    /// <returns>哈希.</returns>
    public static string ComputeHash(string normalizedQuery)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedQuery))).ToLowerInvariant();

    /// <summary>
    /// 复用快速路径：命中聚类且回源校验通过则构建命中返回（未回填文档名/切片序号/上下文，由检索服务既有链路补齐）；
    /// 校验不通过（来源已变化）删除失效聚类并返回 null 回落完整检索.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="query">查询原文.</param>
    /// <param name="top">返回条数上限.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>命中的未回填列表；未命中或已失效返回 null.</returns>
    public async Task<List<WikiSearchHit>?> TryReuseAsync(int wikiId, string query, int top, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeQuery(query);
        if (normalized.Length == 0)
        {
            return null;
        }

        var cluster = await _databaseContext.WikiRecallClusters.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WikiId == wikiId && x.QueryHash == ComputeHash(normalized) && x.IsDeleted == 0, cancellationToken);
        if (cluster == null)
        {
            return null;
        }

        // 索引纪元核验：沉淀后该库发生过任何文档/切片/向量变更（新增、重切割、删除、重嵌）即失效，
        // 删除聚类回落完整检索——保证缓存不吞新增内容（新增文档对已缓存查询可见）
        var currentEpoch = await _databaseContext.Wikis.AsNoTracking()
            .Where(x => x.Id == wikiId)
            .Select(x => x.IndexEpoch)
            .FirstOrDefaultAsync(cancellationToken);
        if (cluster.IndexEpoch != currentEpoch)
        {
            _logger.LogInformation("检索经验聚类纪元过期（沉淀={Recorded}/当前={Current}），删除并回落完整检索. WikiId={WikiId}, ClusterId={ClusterId}", cluster.IndexEpoch, currentEpoch, wikiId, cluster.Id);
            await _databaseContext.WikiRecallClusters
                .Where(x => x.Id == cluster.Id)
                .ExecuteDeleteAsync(cancellationToken);
            return null;
        }

        var stored = ParseHits(cluster.HitsJson);
        if (stored.Count == 0)
        {
            return null;
        }

        // 回源校验：切片仍存活才可复用（chunk 表为真源，来源变化即失效）
        var chunkIds = stored.Select(x => x.ChunkId).Distinct().ToList();
        var chunkRows = await _databaseContext.WikiDocumentChunkContents.AsNoTracking()
            .Where(x => chunkIds.Contains(x.Id) && x.IsDeleted == 0)
            .Select(x => new { x.Id, x.DocumentId, x.SliceContent })
            .ToListAsync(cancellationToken);
        var chunkMap = chunkRows.ToDictionary(x => x.Id);

        // 阈值：存活数不足半数说明来源已明显变化，删除失效聚类（自愈）并回落完整检索
        var threshold = Math.Max(1, stored.Count / 2);
        if (chunkMap.Count < threshold)
        {
            _logger.LogInformation("检索经验聚类已失效（存活 {Survived}/{Stored}），删除并回落完整检索. WikiId={WikiId}, ClusterId={ClusterId}", chunkMap.Count, stored.Count, wikiId, cluster.Id);
            await _databaseContext.WikiRecallClusters
                .Where(x => x.Id == cluster.Id)
                .ExecuteDeleteAsync(cancellationToken);
            return null;
        }

        var hits = new List<WikiSearchHit>();
        foreach (var item in stored)
        {
            if (!chunkMap.TryGetValue(item.ChunkId, out var chunk))
            {
                continue;
            }

            hits.Add(new WikiSearchHit
            {
                WikiId = wikiId,
                DocumentId = chunk.DocumentId,
                ChunkId = item.ChunkId,
                MetadataType = item.MetadataType,
                Content = chunk.SliceContent,
                Score = item.Score,
                RerankScore = item.RerankScore,
            });
            if (hits.Count >= top)
            {
                break;
            }
        }

        return hits;
    }

    /// <summary>
    /// 复用命中后的服务计数（RecallCount+1、刷新最近服务时间；inline 更新不阻塞主流程语义）.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="query">查询原文.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    public async Task RecordReuseAsync(int wikiId, string query, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeQuery(query);
        if (normalized.Length == 0)
        {
            return;
        }

        await _databaseContext.WikiRecallClusters
            .Where(x => x.WikiId == wikiId && x.QueryHash == ComputeHash(normalized) && x.IsDeleted == 0)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.RecallCount, x => x.RecallCount + 1)
                .SetProperty(x => x.LastRecallTime, DateTimeOffset.UtcNow)
                .SetProperty(x => x.UpdateTime, DateTimeOffset.UtcNow), cancellationToken);
    }

    /// <summary>
    /// 沉淀 upsert（消费者调用）：同 (wikiId, queryHash) 存在则刷新命中与计数，否则新建聚类.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="normalizedQuery">归一化查询.</param>
    /// <param name="hits">命中列表（超限截断）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    public async Task UpsertAsync(int wikiId, string normalizedQuery, IReadOnlyList<WikiRecallClusterHit> hits, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(normalizedQuery) || hits.Count == 0)
        {
            return;
        }

        var hash = ComputeHash(normalizedQuery);
        var json = JsonSerializer.Serialize(hits.Take(WikiRecallClusterDefaults.MaxHits).ToList(), JsonOptions);
        var now = DateTimeOffset.UtcNow;

        // 沉淀时读取当前索引纪元（消费时读取比发布时读取更贴近最新状态）
        var epoch = await _databaseContext.Wikis.AsNoTracking()
            .Where(x => x.Id == wikiId)
            .Select(x => x.IndexEpoch)
            .FirstOrDefaultAsync(cancellationToken);

        var updated = await _databaseContext.WikiRecallClusters
            .Where(x => x.WikiId == wikiId && x.QueryHash == hash && x.IsDeleted == 0)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.HitsJson, json)
                .SetProperty(x => x.HitCount, Math.Min(hits.Count, WikiRecallClusterDefaults.MaxHits))
                .SetProperty(x => x.IndexEpoch, epoch)
                .SetProperty(x => x.RecallCount, x => x.RecallCount + 1)
                .SetProperty(x => x.LastRecallTime, now)
                .SetProperty(x => x.UpdateTime, now), cancellationToken);
        if (updated > 0)
        {
            return;
        }

        var entity = new WikiRecallClusterEntity
        {
            Id = _idProvider.NextId(),
            WikiId = wikiId,
            QueryHash = hash,
            Query = normalizedQuery,
            IndexEpoch = epoch,
            HitsJson = json,
            HitCount = Math.Min(hits.Count, WikiRecallClusterDefaults.MaxHits),
            RecallCount = 1,
            LastRecallTime = now,
        };
        await _databaseContext.WikiRecallClusters.AddAsync(entity, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);
    }

    private sealed class StoredHit
    {
        public int DocumentId { get; set; }

        public long ChunkId { get; set; }

        public double? Score { get; set; }

        public double? RerankScore { get; set; }

        public int MetadataType { get; set; }
    }

    private static List<StoredHit> ParseHits(string hitsJson)
    {
        if (string.IsNullOrWhiteSpace(hitsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<StoredHit>>(hitsJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
