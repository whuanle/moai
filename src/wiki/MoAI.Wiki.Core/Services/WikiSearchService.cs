using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.EntityFrameworkCore;
using MoAI.AIChannel.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Services;

/// <summary>
/// 知识库检索实现：按知识库分别用其 embedding 模型生成查询向量，召回后归并.
/// </summary>
[InjectOnScoped]
public class WikiSearchService : IWikiSearchService
{
    private readonly DatabaseContext _databaseContext;
    private readonly IEmbeddingGeneratorProvider _embeddingGeneratorProvider;
    private readonly IWikiEmbeddingVectorStore _vectorStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiSearchService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="embeddingGeneratorProvider">向量生成器提供者.</param>
    /// <param name="vectorStore">知识库向量存储.</param>
    public WikiSearchService(
        DatabaseContext databaseContext,
        IEmbeddingGeneratorProvider embeddingGeneratorProvider,
        IWikiEmbeddingVectorStore vectorStore)
    {
        _databaseContext = databaseContext;
        _embeddingGeneratorProvider = embeddingGeneratorProvider;
        _vectorStore = vectorStore;
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
            .Select(x => new { x.Id, x.TeamId, x.EmbeddingModelId, x.EmbeddingDimensions })
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

            var results = await _vectorStore.SearchAsync(wiki.Id, vector.Value, top, cancellationToken);
            foreach (var result in results)
            {
                hits.Add(new WikiSearchHit
                {
                    WikiId = result.Record.WikiId,
                    DocumentId = result.Record.DocumentId,
                    ChunkId = result.Record.ChunkId,
                    Content = result.Record.Content,
                    Score = result.Score,
                });
            }
        }

        if (hits.Count == 0)
        {
            return hits;
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

        return hits
            .OrderByDescending(x => x.Score ?? double.MinValue)
            .Take(top * ids.Count)
            .ToList();
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
