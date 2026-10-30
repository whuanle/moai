using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.VectorData.PgVector;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.VectorData;
using MoAI.AI.Models;
using MoAI.Database;
using MoAI.Database.Aggregates;

namespace MoAI.Memory.Services;

/// <summary>
/// 基于 Postgres + pgvector 的长期记忆向量存储：每个应用一个集合 __agent_mem_{appId}，
/// 维度取应用生效配置 execution_settings.memory.embeddingDimensions，由 VectorData 提供者按记录定义自动建表.
/// </summary>
public class PgVectorAgentMemoryVectorStore : IMemoryVectorStore
{
    private readonly PostgresVectorStore _vectorStore;
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="PgVectorAgentMemoryVectorStore"/> class.
    /// </summary>
    /// <param name="vectorStore">pgvector 向量库.</param>
    /// <param name="databaseContext">数据库上下文.</param>
    public PgVectorAgentMemoryVectorStore(PostgresVectorStore vectorStore, DatabaseContext databaseContext)
    {
        _vectorStore = vectorStore;
        _databaseContext = databaseContext;
    }

    /// <summary>
    /// 集合（表）名.
    /// </summary>
    /// <param name="appId">应用 id.</param>
    /// <returns>集合名.</returns>
    public static string GetCollectionName(Guid appId) => $"__agent_mem_{appId:N}";

    /// <inheritdoc/>
    public async Task EnsureCollectionAsync(Guid appId, int dimensions, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection(appId, dimensions);
        await EnsureCollectionCompatibleAsync(appId, collection, dimensions, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task UpsertAsync(Guid appId, IReadOnlyList<AgentMemoryVectorRecord> records, CancellationToken cancellationToken = default)
    {
        if (records.Count == 0)
        {
            return;
        }

        var dimensions = records[0].Embedding.Length;
        var collection = GetCollection(appId, dimensions);
        await EnsureCollectionCompatibleAsync(appId, collection, dimensions, cancellationToken);
        await collection.UpsertAsync(records, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid appId, IReadOnlyList<Guid> keys, CancellationToken cancellationToken = default)
    {
        if (keys.Count == 0)
        {
            return;
        }

        var dimensions = await ResolveDimensionsOrZeroAsync(appId, cancellationToken);
        if (dimensions <= 0)
        {
            return;
        }

        var collection = GetCollection(appId, dimensions);
        if (!await collection.CollectionExistsAsync(cancellationToken))
        {
            return;
        }

        await collection.DeleteAsync(keys, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<(AgentMemoryVectorRecord Record, double Score)>> SearchAsync(Guid appId, ReadOnlyMemory<float> queryVector, int top, long userId, IReadOnlyList<int>? types, CancellationToken cancellationToken = default)
    {
        var dimensions = await ResolveDimensionsOrZeroAsync(appId, cancellationToken);
        if (dimensions <= 0)
        {
            return [];
        }

        var collection = GetCollection(appId, dimensions);
        if (!await collection.CollectionExistsAsync(cancellationToken))
        {
            return [];
        }

        return await SearchCollectionAsync(collection, queryVector, top, userId, types, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AgentMemoryVectorRecord>> ListUserAsync(Guid appId, long userId, int max, CancellationToken cancellationToken = default)
    {
        var dimensions = await ResolveDimensionsOrZeroAsync(appId, cancellationToken);
        if (dimensions <= 0)
        {
            return [];
        }

        var collection = GetCollection(appId, dimensions);
        if (!await collection.CollectionExistsAsync(cancellationToken))
        {
            return [];
        }

        var records = new List<AgentMemoryVectorRecord>();
        await foreach (var record in collection.GetAsync(x => x.UserId == userId, max, null, cancellationToken))
        {
            records.Add(record);
        }

        return records;
    }

    /// <inheritdoc/>
    public async Task DeleteCollectionAsync(Guid appId, CancellationToken cancellationToken = default)
    {
        var dimensions = await ResolveDimensionsOrZeroAsync(appId, cancellationToken);
        if (dimensions > 0)
        {
            var collection = GetCollection(appId, dimensions);
            await collection.EnsureCollectionDeletedAsync(cancellationToken);
            return;
        }

        // 配置缺失（应用已删除等）时按集合名兜底：直接 DROP 动态表（表名由 hex guid 构成，无注入风险）
        var tableName = GetCollectionName(appId);
        await _databaseContext.Database.ExecuteSqlRawAsync($"DROP TABLE IF EXISTS {tableName}", cancellationToken);
    }

    /// <inheritdoc/>
    public async Task EnsureGlobalCollectionAsync(int dimensions, CancellationToken cancellationToken = default)
    {
        var collection = GetGlobalCollection(dimensions);
        await EnsureCollectionCompatibleAsync(MemoryGlobalScope.CollectionName, collection, dimensions, isGlobal: true, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task UpsertGlobalAsync(IReadOnlyList<AgentMemoryVectorRecord> records, CancellationToken cancellationToken = default)
    {
        if (records.Count == 0)
        {
            return;
        }

        var dimensions = records[0].Embedding.Length;
        var collection = GetGlobalCollection(dimensions);
        await EnsureCollectionCompatibleAsync(MemoryGlobalScope.CollectionName, collection, dimensions, isGlobal: true, cancellationToken);
        await collection.UpsertAsync(records, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteGlobalAsync(IReadOnlyList<Guid> keys, CancellationToken cancellationToken = default)
    {
        if (keys.Count == 0)
        {
            return;
        }

        var dimensions = await ResolveGlobalDimensionsOrZeroAsync(cancellationToken);
        if (dimensions <= 0)
        {
            return;
        }

        var collection = GetGlobalCollection(dimensions);
        if (!await collection.CollectionExistsAsync(cancellationToken))
        {
            return;
        }

        await collection.DeleteAsync(keys, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<(AgentMemoryVectorRecord Record, double Score)>> SearchGlobalAsync(ReadOnlyMemory<float> queryVector, int top, long userId, IReadOnlyList<int>? types, CancellationToken cancellationToken = default)
    {
        var dimensions = await ResolveGlobalDimensionsOrZeroAsync(cancellationToken);
        if (dimensions <= 0)
        {
            return [];
        }

        var collection = GetGlobalCollection(dimensions);
        if (!await collection.CollectionExistsAsync(cancellationToken))
        {
            return [];
        }

        return await SearchCollectionAsync(collection, queryVector, top, userId, types, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AgentMemoryVectorRecord>> ListGlobalUserAsync(long userId, int max, CancellationToken cancellationToken = default)
    {
        var dimensions = await ResolveGlobalDimensionsOrZeroAsync(cancellationToken);
        if (dimensions <= 0)
        {
            return [];
        }

        var collection = GetGlobalCollection(dimensions);
        if (!await collection.CollectionExistsAsync(cancellationToken))
        {
            return [];
        }

        var records = new List<AgentMemoryVectorRecord>();
        await foreach (var record in collection.GetAsync(x => x.UserId == userId, max, null, cancellationToken))
        {
            records.Add(record);
        }

        return records;
    }

    /// <inheritdoc/>
    public async Task DeleteGlobalCollectionAsync(CancellationToken cancellationToken = default)
    {
        var dimensions = await ResolveGlobalDimensionsOrZeroAsync(cancellationToken);
        if (dimensions > 0)
        {
            var collection = GetGlobalCollection(dimensions);
            await collection.EnsureCollectionDeletedAsync(cancellationToken);
            return;
        }

        await _databaseContext.Database.ExecuteSqlRawAsync($"DROP TABLE IF EXISTS {MemoryGlobalScope.CollectionName}", cancellationToken);
    }

    /// <summary>
    /// 集合维度兼容检查：表不存在时直接建表；已存在但维度与请求不一致时，空表重建（更换模型/维度后首写自愈）、
    /// 非空表抛错（向量维度锁定，须先清空应用全部记忆）.
    /// </summary>
    private async Task EnsureCollectionCompatibleAsync(Guid appId, VectorStoreCollection<Guid, AgentMemoryVectorRecord> collection, int dimensions, CancellationToken cancellationToken)
        => await EnsureCollectionCompatibleAsync(GetCollectionName(appId), collection, dimensions, isGlobal: false, cancellationToken);

    /// <summary>
    /// 集合维度兼容检查（全局/应用通用）：表不存在时直接建表；已存在但维度与请求不一致时，空表重建、非空表抛错.
    /// </summary>
    private async Task EnsureCollectionCompatibleAsync(string tableName, VectorStoreCollection<Guid, AgentMemoryVectorRecord> collection, int dimensions, bool isGlobal, CancellationToken cancellationToken)
    {
        if (!await collection.CollectionExistsAsync(cancellationToken))
        {
            await collection.EnsureCollectionExistsAsync(cancellationToken);
            return;
        }

        var existing = await ResolveTableDimensionsAsync(tableName, cancellationToken);
        if (existing <= 0 || existing == dimensions)
        {
            return;
        }

        var hasRows = await _databaseContext.Database
            .SqlQueryRaw<bool>("SELECT EXISTS(SELECT 1 FROM " + tableName + ") AS \"Value\"")
            .FirstOrDefaultAsync(cancellationToken);
        if (hasRows)
        {
            throw isGlobal
                ? new InvalidOperationException($"全局记忆向量表维度({existing})与当前配置({dimensions})不一致，请先清空全部全局记忆再更换向量化模型或维度.")
                : new InvalidOperationException($"应用记忆向量表维度({existing})与当前配置({dimensions})不一致，请先清空应用全部记忆再更换向量化模型或维度.");
        }

        await collection.EnsureCollectionDeletedAsync(cancellationToken);
        await collection.EnsureCollectionExistsAsync(cancellationToken);
    }

    /// <summary>
    /// 读取集合向量列的维度（pgvector 的 atttypmod 即维度）；表不存在或解析不到返回 0.
    /// </summary>
    private async Task<int> ResolveTableDimensionsAsync(Guid appId, CancellationToken cancellationToken)
        => await ResolveTableDimensionsAsync(GetCollectionName(appId), cancellationToken);

    /// <summary>
    /// 读取集合向量列的维度（按表名）；表不存在或解析不到返回 0.
    /// </summary>
    private async Task<int> ResolveTableDimensionsAsync(string tableName, CancellationToken cancellationToken)
    {
        // 表名由 appId 的 hex 或固定常量构成（无引号歧义/注入面），SqlQueryRaw 不做插值参数化，须以纯字符串内联
        return await _databaseContext.Database
            .SqlQueryRaw<int>("SELECT a.atttypmod AS \"Value\" FROM pg_attribute a JOIN pg_class c ON c.oid = a.attrelid JOIN pg_type t ON t.oid = a.atttypid WHERE c.relname = '" + tableName + "' AND t.typname = 'vector' AND a.attnum > 0 AND NOT a.attisdropped LIMIT 1")
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// 全局集合检索过滤复用（按用户与可选类型）.
    /// </summary>
    private static async Task<IReadOnlyList<(AgentMemoryVectorRecord Record, double Score)>> SearchCollectionAsync(VectorStoreCollection<Guid, AgentMemoryVectorRecord> collection, ReadOnlyMemory<float> queryVector, int top, long userId, IReadOnlyList<int>? types, CancellationToken cancellationToken)
    {
        var hasTypes = types is { Count: > 0 };
        var options = hasTypes
            ? new VectorSearchOptions<AgentMemoryVectorRecord>
            {
                Filter = x => x.UserId == userId && types!.Contains(x.MemoryType),
            }
            : new VectorSearchOptions<AgentMemoryVectorRecord>
            {
                Filter = x => x.UserId == userId,
            };

        var results = new List<(AgentMemoryVectorRecord, double)>();
        await foreach (var result in collection.SearchAsync<ReadOnlyMemory<float>>(queryVector, top, options, cancellationToken))
        {
            results.Add((result.Record, (double)(result.Score ?? 0f)));
        }

        return results;
    }

    private static VectorStoreCollectionDefinition BuildDefinition(int dimensions) => new()
    {
        Properties =
        [
            new VectorStoreKeyProperty(nameof(AgentMemoryVectorRecord.Key), typeof(Guid)),
            new VectorStoreDataProperty(nameof(AgentMemoryVectorRecord.UserId), typeof(long)) { IsIndexed = true },
            new VectorStoreDataProperty(nameof(AgentMemoryVectorRecord.MemoryType), typeof(int)) { IsIndexed = true },
            new VectorStoreDataProperty(nameof(AgentMemoryVectorRecord.Content), typeof(string)),
            new VectorStoreDataProperty(nameof(AgentMemoryVectorRecord.SourceSessionId), typeof(Guid)),
            new VectorStoreDataProperty(nameof(AgentMemoryVectorRecord.CreateTimeMs), typeof(long)),
            new VectorStoreDataProperty(nameof(AgentMemoryVectorRecord.UpdateTimeMs), typeof(long)),
            new VectorStoreVectorProperty(nameof(AgentMemoryVectorRecord.Embedding), typeof(ReadOnlyMemory<float>), dimensions)
            {
                DistanceFunction = DistanceFunction.CosineSimilarity,
                IndexKind = IndexKind.Hnsw,
            },
        ],
    };

    private VectorStoreCollection<Guid, AgentMemoryVectorRecord> GetCollection(Guid appId, int dimensions)
        => _vectorStore.GetCollection<Guid, AgentMemoryVectorRecord>(GetCollectionName(appId), BuildDefinition(dimensions));

    private VectorStoreCollection<Guid, AgentMemoryVectorRecord> GetGlobalCollection(int dimensions)
        => _vectorStore.GetCollection<Guid, AgentMemoryVectorRecord>(MemoryGlobalScope.CollectionName, BuildDefinition(dimensions));

    private async Task<int> ResolveGlobalDimensionsOrZeroAsync(CancellationToken cancellationToken)
        => await ResolveTableDimensionsAsync(MemoryGlobalScope.CollectionName, cancellationToken);

    private async Task<int> ResolveDimensionsOrZeroAsync(Guid appId, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps.AsNoTracking()
            .Where(x => x.Id == appId)
            .Select(x => new { x.Id, x.TeamId, x.AppType, x.PublishStatus })
            .FirstOrDefaultAsync(cancellationToken);
        if (app == null)
        {
            return 0;
        }

        var config = await _databaseContext.AppAgentConfigs.AsNoTracking()
            .Where(x => x.AppId == appId)
            .Select(x => new { x.ExecutionSettings, x.PublishedConfig })
            .FirstOrDefaultAsync(cancellationToken);
        if (config == null)
        {
            return 0;
        }

        var effective = AppAgentConfigSnapshot.ResolveEffectiveConfig(
            new Database.Entities.AppEntity { Id = app.Id, TeamId = app.TeamId, AppType = app.AppType, PublishStatus = app.PublishStatus },
            new Database.Entities.AppAgentConfigEntity { AppId = app.Id, TeamId = app.TeamId, ExecutionSettings = config.ExecutionSettings, PublishedConfig = config.PublishedConfig },
            preferPublished: true);
        var settings = AgentMemorySettings.ParseExecutionSettings(effective.ExecutionSettings);
        if (settings is not { Enabled: true } || settings.EmbeddingDimensions <= 0)
        {
            return 0;
        }

        return settings.EmbeddingDimensions;
    }
}
