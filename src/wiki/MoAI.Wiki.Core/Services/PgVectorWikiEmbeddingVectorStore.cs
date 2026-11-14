using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.VectorData.PgVector;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.VectorData;
using MoAI.Database;
using Npgsql;

namespace MoAI.Wiki.Services;

/// <summary>
/// 基于 Postgres + pgvector 的向量存储：每个知识库一个集合 __wiki_{id}，由 VectorData 提供者按记录定义自动建表.
/// </summary>
public class PgVectorWikiEmbeddingVectorStore : IWikiEmbeddingVectorStore
{
    private readonly PostgresVectorStore _vectorStore;
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="PgVectorWikiEmbeddingVectorStore"/> class.
    /// </summary>
    /// <param name="vectorStore">pgvector 向量库.</param>
    /// <param name="databaseContext">数据库上下文.</param>
    public PgVectorWikiEmbeddingVectorStore(PostgresVectorStore vectorStore, DatabaseContext databaseContext)
    {
        _vectorStore = vectorStore;
        _databaseContext = databaseContext;
    }

    /// <summary>
    /// 集合（表）名.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <returns>集合名.</returns>
    public static string GetCollectionName(int wikiId) => $"__wiki_{wikiId}";

    /// <summary>
    /// 临时集合（表）名：模型迁移期间新向量先写入临时表，全部完成后换名顶替正式表.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <returns>临时集合名.</returns>
    public static string GetTempCollectionName(int wikiId) => $"__wiki_mig_{wikiId}";

    /// <summary>
    /// 集合建表互斥锁（按 wikiId）：并发向量化（MQ Qos=4）同时 EnsureCollectionExistsAsync 会对同一表名
    /// 竞态 CREATE TABLE，触发 pg_type 目录唯一索引冲突（23505）.
    /// </summary>
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> CollectionLocks = new();

    /// <inheritdoc/>
    public async Task EnsureCollectionAsync(int wikiId, int dimensions, CancellationToken cancellationToken = default)
    {
        await EnsureCollectionLockedAsync(wikiId, dimensions, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ReplaceDocumentVectorsAsync(int wikiId, int documentId, IReadOnlyList<WikiEmbeddingVectorRecord> records, CancellationToken cancellationToken = default)
    {
        var dimensions = await ResolveDimensionsAsync(wikiId, cancellationToken);
        var collection = GetCollection(wikiId, dimensions);
        await EnsureCollectionLockedAsync(wikiId, dimensions, cancellationToken);
        await DeleteByDocumentAsync(collection, documentId, cancellationToken);
        if (records.Count > 0)
        {
            await collection.UpsertAsync(records, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task DeleteDocumentVectorsAsync(int wikiId, int documentId, CancellationToken cancellationToken = default)
    {
        var dimensions = await ResolveDimensionsOrZeroAsync(wikiId, cancellationToken);
        if (dimensions <= 0)
        {
            return;
        }

        var collection = GetCollection(wikiId, dimensions);
        if (!await collection.CollectionExistsAsync(cancellationToken))
        {
            return;
        }

        await DeleteByDocumentAsync(collection, documentId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteWikiVectorsAsync(int wikiId, CancellationToken cancellationToken = default)
    {
        // 优先走提供者的整集合删除；配置缺失（wiki 已删）时按表名兜底 DROP（表名由 wikiId 构成，无注入面）
        var dimensions = await ResolveDimensionsOrZeroAsync(wikiId, cancellationToken);
        if (dimensions > 0)
        {
            var collection = GetCollection(wikiId, dimensions);
            if (await collection.CollectionExistsAsync(cancellationToken))
            {
                await collection.EnsureCollectionDeletedAsync(cancellationToken);
            }

            return;
        }

        await _databaseContext.Database.ExecuteSqlRawAsync($"DROP TABLE IF EXISTS {GetCollectionName(wikiId)}", cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> CountDocumentVectorsAsync(int wikiId, int documentId, CancellationToken cancellationToken = default)
    {
        var dimensions = await ResolveDimensionsOrZeroAsync(wikiId, cancellationToken);
        if (dimensions <= 0 || !await CollectionTableExistsAsync(wikiId, dimensions, cancellationToken))
        {
            return 0;
        }

        // DocumentId 为 int 参数（EF 参数绑定），仅表名按纯字符串内联
        return await _databaseContext.Database
            .SqlQueryRaw<int>($"SELECT COUNT(*)::int AS \"Value\" FROM {GetCollectionName(wikiId)} WHERE \"DocumentId\" = @documentId", new NpgsqlParameter("documentId", documentId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> CountCollectionAsync(int wikiId, CancellationToken cancellationToken = default)
    {
        var dimensions = await ResolveDimensionsOrZeroAsync(wikiId, cancellationToken);
        if (dimensions <= 0)
        {
            return 0;
        }

        return await _databaseContext.Database
            .SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM " + GetCollectionName(wikiId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WikiEmbeddingVectorRecord>> ListAllPageAsync(int wikiId, int skip, int max, CancellationToken cancellationToken = default)
    {
        var dimensions = await ResolveTableDimensionsAsync(GetCollectionName(wikiId), cancellationToken);
        if (dimensions <= 0 || !await CollectionTableExistsAsync(wikiId, dimensions, cancellationToken))
        {
            return [];
        }

        var collection = GetCollection(wikiId, dimensions);

        // 按 Key（唯一）排序 + skip 分页，保证翻页期间记录不重不漏（迁移期间原集合只读）；
        // 提供者不接受 null 过滤，用恒真等值条件表达全表遍历
        var records = new List<WikiEmbeddingVectorRecord>();
        var options = new FilteredRecordRetrievalOptions<WikiEmbeddingVectorRecord>
        {
            Skip = skip,
            OrderBy = definition => definition.Ascending(x => x.Key),
        };
        await foreach (var record in collection.GetAsync(x => x.WikiId == x.WikiId, max, options, cancellationToken))
        {
            records.Add(record);
        }

        return records;
    }

    /// <inheritdoc/>
    public async Task BeginMigrationCollectionAsync(int wikiId, int dimensions, CancellationToken cancellationToken = default)
    {
        // 兜底删除上次失败/崩溃残留的临时表，保证重建起点干净
        await _databaseContext.Database.ExecuteSqlRawAsync($"DROP TABLE IF EXISTS {GetTempCollectionName(wikiId)}", cancellationToken);
        var collection = GetTempCollection(wikiId, dimensions);
        await collection.EnsureCollectionExistsAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task UpsertMigrationCollectionAsync(int wikiId, IReadOnlyList<WikiEmbeddingVectorRecord> records, int dimensions, CancellationToken cancellationToken = default)
    {
        if (records.Count == 0)
        {
            return;
        }

        var collection = GetTempCollection(wikiId, dimensions);
        await collection.UpsertAsync(records, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task CompleteMigrationCollectionAsync(int wikiId, CancellationToken cancellationToken = default)
    {
        var tableName = GetCollectionName(wikiId);
        var tempName = GetTempCollectionName(wikiId);

        // 换名窗口极短且迁移期间该库向量化写入已被阻断；旧表删除后临时表立即顶上
        await using var transaction = await _databaseContext.Database.BeginTransactionAsync(cancellationToken);
        await _databaseContext.Database.ExecuteSqlRawAsync($"DROP TABLE IF EXISTS {tableName}", cancellationToken);
        await _databaseContext.Database.ExecuteSqlRawAsync($"ALTER TABLE {tempName} RENAME TO {tableName}", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task AbortMigrationCollectionAsync(int wikiId, CancellationToken cancellationToken = default)
    {
        await _databaseContext.Database.ExecuteSqlRawAsync($"DROP TABLE IF EXISTS {GetTempCollectionName(wikiId)}", cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WikiEmbeddingSearchResult>> SearchAsync(int wikiId, ReadOnlyMemory<float> queryVector, int top, IReadOnlyCollection<int>? documentIds = null, CancellationToken cancellationToken = default)
    {
        var dimensions = await ResolveDimensionsAsync(wikiId, cancellationToken);
        var collection = GetCollection(wikiId, dimensions);
        if (!await collection.CollectionExistsAsync(cancellationToken))
        {
            return [];
        }

        var options = new VectorSearchOptions<WikiEmbeddingVectorRecord>();
        if (documentIds is { Count: > 0 })
        {
            var ids = documentIds.ToList();
            options.Filter = x => ids.Contains(x.DocumentId);
        }

        var results = new List<WikiEmbeddingSearchResult>();
        await foreach (var result in collection.SearchAsync<ReadOnlyMemory<float>>(queryVector, top, options, cancellationToken))
        {
            results.Add(new WikiEmbeddingSearchResult
            {
                Record = result.Record,
                Score = result.Score,
            });
        }

        return results;
    }

    private static async Task DeleteByDocumentAsync(VectorStoreCollection<Guid, WikiEmbeddingVectorRecord> collection, int documentId, CancellationToken cancellationToken)
    {
        var keys = new List<Guid>();
        await foreach (var record in collection.GetAsync(x => x.DocumentId == documentId, int.MaxValue, null, cancellationToken))
        {
            keys.Add(record.Key);
        }

        if (keys.Count > 0)
        {
            await collection.DeleteAsync(keys, cancellationToken);
        }
    }

    private static VectorStoreCollectionDefinition BuildDefinition(int dimensions) => new()
    {
        Properties =
        [
            new VectorStoreKeyProperty(nameof(WikiEmbeddingVectorRecord.Key), typeof(Guid)),
            new VectorStoreDataProperty(nameof(WikiEmbeddingVectorRecord.WikiId), typeof(int)) { IsIndexed = true },
            new VectorStoreDataProperty(nameof(WikiEmbeddingVectorRecord.DocumentId), typeof(int)) { IsIndexed = true },
            new VectorStoreDataProperty(nameof(WikiEmbeddingVectorRecord.ChunkId), typeof(long)) { IsIndexed = true },
            new VectorStoreDataProperty(nameof(WikiEmbeddingVectorRecord.MetadataType), typeof(int)) { IsIndexed = true },
            new VectorStoreDataProperty(nameof(WikiEmbeddingVectorRecord.Content), typeof(string)),
            new VectorStoreVectorProperty(nameof(WikiEmbeddingVectorRecord.Embedding), typeof(ReadOnlyMemory<float>), dimensions)
            {
                DistanceFunction = DistanceFunction.CosineSimilarity,
                IndexKind = IndexKind.Hnsw,
            },
        ],
    };

    private VectorStoreCollection<Guid, WikiEmbeddingVectorRecord> GetCollection(int wikiId, int dimensions)
        => _vectorStore.GetCollection<Guid, WikiEmbeddingVectorRecord>(GetCollectionName(wikiId), BuildDefinition(dimensions));

    private VectorStoreCollection<Guid, WikiEmbeddingVectorRecord> GetTempCollection(int wikiId, int dimensions)
        => _vectorStore.GetCollection<Guid, WikiEmbeddingVectorRecord>(GetTempCollectionName(wikiId), BuildDefinition(dimensions));

    /// <summary>
    /// 读取集合向量列的维度（pgvector 的 atttypmod 即维度）；表不存在或解析不到返回 0（迁移期间不依赖 wiki 配置，直接读表）.
    /// </summary>
    private async Task<int> ResolveTableDimensionsAsync(string tableName, CancellationToken cancellationToken)
    {
        // 表名由 wikiId 构成（无引号歧义/注入面），SqlQueryRaw 不做插值参数化，须以纯字符串内联
        return await _databaseContext.Database
            .SqlQueryRaw<int>("SELECT a.atttypmod AS \"Value\" FROM pg_attribute a JOIN pg_class c ON c.oid = a.attrelid JOIN pg_type t ON t.oid = a.atttypid WHERE c.relname = '" + tableName + "' AND t.typname = 'vector' AND a.attnum > 0 AND NOT a.attisdropped LIMIT 1")
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<bool> CollectionTableExistsAsync(int wikiId, int dimensions, CancellationToken cancellationToken)
    {
        var collection = GetCollection(wikiId, dimensions);
        return await collection.CollectionExistsAsync(cancellationToken);
    }

    private async Task<int> ResolveDimensionsAsync(int wikiId, CancellationToken cancellationToken)
    {
        var dimensions = await ResolveDimensionsOrZeroAsync(wikiId, cancellationToken);
        if (dimensions <= 0)
        {
            throw new InvalidOperationException($"知识库未配置向量维度: {wikiId}");
        }

        return dimensions;
    }

    private Task<int> ResolveDimensionsOrZeroAsync(int wikiId, CancellationToken cancellationToken)
        => _databaseContext.Wikis
            .Where(x => x.Id == wikiId)
            .Select(x => x.EmbeddingDimensions)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// 确保集合存在（按 wiki 互斥）：并发 CREATE TABLE 会触发 pg_type 目录唯一索引冲突，
    /// 同 wiki 串行化；跨进程残留竞态以 23505 单次等待重试兜底.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="dimensions">向量维度.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    private async Task EnsureCollectionLockedAsync(int wikiId, int dimensions, CancellationToken cancellationToken)
    {
        var semaphore = CollectionLocks.GetOrAdd(wikiId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            var collection = GetCollection(wikiId, dimensions);
            try
            {
                await collection.EnsureCollectionExistsAsync(cancellationToken);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                // 跨进程/跨实例同时建同一表的残留竞态：等待对方提交后重试一次
                await Task.Delay(500, cancellationToken);
                await collection.EnsureCollectionExistsAsync(cancellationToken);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }
}
