using Microsoft.Extensions.Logging;
using MoAI;
using MoAI.KnowledgeGraph.Models;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 接入图内省缓存（Redis）：`kg:introspect:{id}:{db}` 短 TTL 热缓存，`kg:introspect:baseline:{id}:{db}` 长期基线.
/// </summary>
public class KnowledgeGraphIntrospectionCache : IKnowledgeGraphIntrospectionCache
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly IKnowledgeGraphStore _store;
    private readonly IRedisDatabase _redisDatabase;
    private readonly ILogger<KnowledgeGraphIntrospectionCache> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="KnowledgeGraphIntrospectionCache"/> class.
    /// </summary>
    /// <param name="store">图存储.</param>
    /// <param name="redisDatabase">Redis 数据库.</param>
    /// <param name="logger">日志.</param>
    public KnowledgeGraphIntrospectionCache(IKnowledgeGraphStore store, IRedisDatabase redisDatabase, ILogger<KnowledgeGraphIntrospectionCache> logger)
    {
        _store = store;
        _redisDatabase = redisDatabase;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<(KnowledgeGraphIntrospection Introspection, KnowledgeGraphIntrospectionDiff? Changes, bool FromCache)> GetAsync(long knowledgeGraphId, string database, bool refresh, CancellationToken cancellationToken)
    {
        var cacheKey = CacheKey(knowledgeGraphId, database);
        var baselineKey = BaselineKey(knowledgeGraphId, database);

        if (!refresh)
        {
            var cached = await _redisDatabase.GetAsync<KnowledgeGraphIntrospection>(cacheKey);
            if (cached != null)
            {
                return (cached, null, true);
            }
        }

        var introspection = await _store.IntrospectAsync(database, cancellationToken);

        KnowledgeGraphIntrospectionDiff? diff = null;
        var baseline = await _redisDatabase.GetAsync<KnowledgeGraphIntrospection>(baselineKey);
        if (baseline != null)
        {
            diff = Diff(baseline, introspection);
        }

        await _redisDatabase.Database.StringSetAsync(baselineKey, introspection.ToRedisValue());
        await _redisDatabase.Database.StringSetAsync(cacheKey, introspection.ToRedisValue(), CacheTtl);
        _logger.LogInformation("接入图 {KnowledgeGraphId} 内省刷新：标签 {LabelCount} 个，关系类型 {RelationTypeCount} 个。", knowledgeGraphId, introspection.Labels.Count, introspection.RelationshipTypes.Count);

        return (introspection, diff, false);
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(long knowledgeGraphId, string database, CancellationToken cancellationToken)
    {
        await _redisDatabase.Database.KeyDeleteAsync(CacheKey(knowledgeGraphId, database));
        await _redisDatabase.Database.KeyDeleteAsync(BaselineKey(knowledgeGraphId, database));
    }

    private static string CacheKey(long knowledgeGraphId, string database)
        => $"kg:introspect:{knowledgeGraphId}:{database}";

    private static string BaselineKey(long knowledgeGraphId, string database)
        => $"kg:introspect:baseline:{knowledgeGraphId}:{database}";

    private static KnowledgeGraphIntrospectionDiff Diff(KnowledgeGraphIntrospection baseline, KnowledgeGraphIntrospection current)
    {
        var oldLabels = baseline.Labels.Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        var newLabels = current.Labels.Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        var oldRelations = baseline.RelationshipTypes.Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        var newRelations = current.RelationshipTypes.Select(x => x.Name).ToHashSet(StringComparer.Ordinal);

        return new KnowledgeGraphIntrospectionDiff(
            newLabels.Except(oldLabels).OrderBy(x => x, StringComparer.Ordinal).ToList(),
            oldLabels.Except(newLabels).OrderBy(x => x, StringComparer.Ordinal).ToList(),
            newRelations.Except(oldRelations).OrderBy(x => x, StringComparer.Ordinal).ToList(),
            oldRelations.Except(newRelations).OrderBy(x => x, StringComparer.Ordinal).ToList());
    }
}
