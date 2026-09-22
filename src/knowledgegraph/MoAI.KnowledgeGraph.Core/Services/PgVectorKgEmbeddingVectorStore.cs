using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.VectorData.PgVector;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.VectorData;
using MoAI.Database;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 基于 Postgres + pgvector 的向量存储：每个图谱一个集合 __kg_{id}，由 VectorData 提供者按记录定义自动建表.
/// </summary>
public class PgVectorKgEmbeddingVectorStore : IKgEmbeddingVectorStore
{
    private readonly PostgresVectorStore _vectorStore;
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="PgVectorKgEmbeddingVectorStore"/> class.
    /// </summary>
    /// <param name="vectorStore">pgvector 向量库.</param>
    /// <param name="databaseContext">数据库上下文.</param>
    public PgVectorKgEmbeddingVectorStore(PostgresVectorStore vectorStore, DatabaseContext databaseContext)
    {
        _vectorStore = vectorStore;
        _databaseContext = databaseContext;
    }

    /// <summary>
    /// 集合（表）名.
    /// </summary>
    /// <param name="kgId">图谱 id.</param>
    /// <returns>集合名.</returns>
    public static string GetCollectionName(int kgId) => $"__kg_{kgId}";

    /// <inheritdoc/>
    public async Task EnsureCollectionAsync(int kgId, int dimensions, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection(kgId, dimensions);
        await collection.EnsureCollectionExistsAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ReplaceNodeVectorsAsync(int kgId, string nodeId, IReadOnlyList<KgEmbeddingVectorRecord> records, CancellationToken cancellationToken = default)
    {
        var dimensions = await ResolveDimensionsAsync(kgId, cancellationToken);
        var collection = GetCollection(kgId, dimensions);
        await collection.EnsureCollectionExistsAsync(cancellationToken);
        await DeleteByNodeAsync(collection, nodeId, cancellationToken);
        if (records.Count > 0)
        {
            await collection.UpsertAsync(records, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task DeleteNodeVectorsAsync(int kgId, string nodeId, CancellationToken cancellationToken = default)
    {
        var dimensions = await ResolveDimensionsOrZeroAsync(kgId, cancellationToken);
        if (dimensions <= 0)
        {
            return;
        }

        var collection = GetCollection(kgId, dimensions);
        if (!await collection.CollectionExistsAsync(cancellationToken))
        {
            return;
        }

        await DeleteByNodeAsync(collection, nodeId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteGraphVectorsAsync(int kgId, CancellationToken cancellationToken = default)
    {
        var dimensions = await ResolveDimensionsOrZeroAsync(kgId, cancellationToken);
        if (dimensions <= 0)
        {
            return;
        }

        var collection = GetCollection(kgId, dimensions);
        await collection.EnsureCollectionDeletedAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<KgEmbeddingSearchResult>> SearchAsync(int kgId, ReadOnlyMemory<float> queryVector, int top, CancellationToken cancellationToken = default)
    {
        var dimensions = await ResolveDimensionsAsync(kgId, cancellationToken);
        var collection = GetCollection(kgId, dimensions);
        if (!await collection.CollectionExistsAsync(cancellationToken))
        {
            return [];
        }

        var options = new VectorSearchOptions<KgEmbeddingVectorRecord>();
        var results = new List<KgEmbeddingSearchResult>();
        await foreach (var result in collection.SearchAsync<ReadOnlyMemory<float>>(queryVector, top, options, cancellationToken))
        {
            results.Add(new KgEmbeddingSearchResult
            {
                Record = result.Record,
                Score = result.Score,
            });
        }

        return results;
    }

    private static async Task DeleteByNodeAsync(VectorStoreCollection<Guid, KgEmbeddingVectorRecord> collection, string nodeId, CancellationToken cancellationToken)
    {
        var keys = new List<Guid>();
        await foreach (var record in collection.GetAsync(x => x.NodeId == nodeId, int.MaxValue, null, cancellationToken))
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
            new VectorStoreKeyProperty(nameof(KgEmbeddingVectorRecord.Key), typeof(Guid)),
            new VectorStoreDataProperty(nameof(KgEmbeddingVectorRecord.KgId), typeof(int)) { IsIndexed = true },
            new VectorStoreDataProperty(nameof(KgEmbeddingVectorRecord.NodeId), typeof(string)) { IsIndexed = true },
            new VectorStoreDataProperty(nameof(KgEmbeddingVectorRecord.EntityTypeId), typeof(long)) { IsIndexed = true },
            new VectorStoreDataProperty(nameof(KgEmbeddingVectorRecord.Name), typeof(string)),
            new VectorStoreDataProperty(nameof(KgEmbeddingVectorRecord.Content), typeof(string)),
            new VectorStoreVectorProperty(nameof(KgEmbeddingVectorRecord.Embedding), typeof(ReadOnlyMemory<float>), dimensions)
            {
                DistanceFunction = DistanceFunction.CosineSimilarity,
                IndexKind = IndexKind.Hnsw,
            },
        ],
    };

    private VectorStoreCollection<Guid, KgEmbeddingVectorRecord> GetCollection(int kgId, int dimensions)
        => _vectorStore.GetCollection<Guid, KgEmbeddingVectorRecord>(GetCollectionName(kgId), BuildDefinition(dimensions));

    private async Task<int> ResolveDimensionsAsync(int kgId, CancellationToken cancellationToken)
    {
        var dimensions = await ResolveDimensionsOrZeroAsync(kgId, cancellationToken);
        if (dimensions <= 0)
        {
            throw new InvalidOperationException($"知识图谱未配置向量维度: {kgId}");
        }

        return dimensions;
    }

    private Task<int> ResolveDimensionsOrZeroAsync(int kgId, CancellationToken cancellationToken)
        => _databaseContext.KnowledgeGraphs
            .Where(x => x.Id == kgId)
            .Select(x => x.EmbeddingDimensions)
            .FirstOrDefaultAsync(cancellationToken);
}
