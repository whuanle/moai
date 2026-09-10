using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.VectorData.PgVector;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.VectorData;
using MoAI.Database;

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

    /// <inheritdoc/>
    public async Task EnsureCollectionAsync(int wikiId, int dimensions, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection(wikiId, dimensions);
        await collection.EnsureCollectionExistsAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ReplaceDocumentVectorsAsync(int wikiId, int documentId, IReadOnlyList<WikiEmbeddingVectorRecord> records, CancellationToken cancellationToken = default)
    {
        var dimensions = await ResolveDimensionsAsync(wikiId, cancellationToken);
        var collection = GetCollection(wikiId, dimensions);
        await collection.EnsureCollectionExistsAsync(cancellationToken);
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
    public async Task<int> CountDocumentVectorsAsync(int wikiId, int documentId, CancellationToken cancellationToken = default)
    {
        var dimensions = await ResolveDimensionsOrZeroAsync(wikiId, cancellationToken);
        if (dimensions <= 0)
        {
            return 0;
        }

        var collection = GetCollection(wikiId, dimensions);
        if (!await collection.CollectionExistsAsync(cancellationToken))
        {
            return 0;
        }

        var count = 0;
        await foreach (var record in collection.GetAsync(x => x.DocumentId == documentId, int.MaxValue, null, cancellationToken))
        {
            _ = record;
            count++;
        }

        return count;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WikiEmbeddingSearchResult>> SearchAsync(int wikiId, ReadOnlyMemory<float> queryVector, int top, CancellationToken cancellationToken = default)
    {
        var dimensions = await ResolveDimensionsAsync(wikiId, cancellationToken);
        var collection = GetCollection(wikiId, dimensions);
        if (!await collection.CollectionExistsAsync(cancellationToken))
        {
            return [];
        }

        var results = new List<WikiEmbeddingSearchResult>();
        await foreach (var result in collection.SearchAsync<ReadOnlyMemory<float>>(queryVector, top, null, cancellationToken))
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
}
