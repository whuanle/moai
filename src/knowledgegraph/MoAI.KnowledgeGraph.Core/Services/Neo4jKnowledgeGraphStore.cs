using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Models;
using Neo4j.Driver;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 基于 Neo4j 的知识图谱存储.
/// </summary>
public sealed class Neo4jKnowledgeGraphStore : IKnowledgeGraphStore
{
    private const string NodeReturn = "n.id AS id, n.KnowledgeGraphId AS KnowledgeGraphId, n.entityTypeId AS entityTypeId, n.name AS name, n.description AS description";
    private const string EdgeReturn = "r.id AS id, r.KnowledgeGraphId AS KnowledgeGraphId, r.relationTypeId AS relationTypeId, s.id AS sourceNodeId, t.id AS targetNodeId";

    private readonly Neo4jDriverProvider _provider;

    /// <summary>
    /// Initializes a new instance of the <see cref="Neo4jKnowledgeGraphStore"/> class.
    /// </summary>
    /// <param name="provider">Neo4j 驱动提供者.</param>
    public Neo4jKnowledgeGraphStore(Neo4jDriverProvider provider)
    {
        _provider = provider;
    }

    /// <inheritdoc/>
    public async Task<int> CountNodesByEntityTypeAsync(long KnowledgeGraphId, long entityTypeId, CancellationToken cancellationToken)
    {
        var records = await ReadAsync(
            "MATCH (n:KgNode {KnowledgeGraphId: $KnowledgeGraphId, entityTypeId: $entityTypeId}) RETURN count(n) AS c",
            new { KnowledgeGraphId, entityTypeId },
            cancellationToken);
        return (int)records[0]["c"].As<long>();
    }

    /// <inheritdoc/>
    public async Task<int> CountEdgesByRelationTypeAsync(long KnowledgeGraphId, long relationTypeId, CancellationToken cancellationToken)
    {
        var records = await ReadAsync(
            "MATCH ()-[r:KG_REL {KnowledgeGraphId: $KnowledgeGraphId, relationTypeId: $relationTypeId}]->() RETURN count(r) AS c",
            new { KnowledgeGraphId, relationTypeId },
            cancellationToken);
        return (int)records[0]["c"].As<long>();
    }

    /// <inheritdoc/>
    public async Task<KnowledgeGraphNodeRecord> CreateNodeAsync(long KnowledgeGraphId, long entityTypeId, string name, string description, CancellationToken cancellationToken)
    {
        await _provider.EnsureInitializedAsync(cancellationToken);
        var id = Guid.CreateVersion7().ToString();
        await WriteAsync(
            "CREATE (n:KgNode {id: $id, KnowledgeGraphId: $KnowledgeGraphId, entityTypeId: $entityTypeId, name: $name, description: $description})",
            new { id, KnowledgeGraphId, entityTypeId, name, description },
            cancellationToken);
        return new KnowledgeGraphNodeRecord(id, KnowledgeGraphId, entityTypeId, name, description);
    }

    /// <inheritdoc/>
    public async Task UpdateNodeAsync(long KnowledgeGraphId, string nodeId, long entityTypeId, string name, string description, CancellationToken cancellationToken)
    {
        await WriteAsync(
            "MATCH (n:KgNode {KnowledgeGraphId: $KnowledgeGraphId, id: $id}) SET n.entityTypeId = $entityTypeId, n.name = $name, n.description = $description",
            new { KnowledgeGraphId, id = nodeId, entityTypeId, name, description },
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteNodeAsync(long KnowledgeGraphId, string nodeId, CancellationToken cancellationToken)
    {
        var records = await WriteReadAsync(
            "MATCH (n:KgNode {KnowledgeGraphId: $KnowledgeGraphId, id: $id}) DETACH DELETE n RETURN count(*) AS c",
            new { KnowledgeGraphId, id = nodeId },
            cancellationToken);
        return records.Count > 0 && records[0]["c"].As<long>() > 0;
    }

    /// <inheritdoc/>
    public async Task<KnowledgeGraphNodeRecord?> GetNodeAsync(long KnowledgeGraphId, string nodeId, CancellationToken cancellationToken)
    {
        var records = await ReadAsync(
            $"MATCH (n:KgNode {{KnowledgeGraphId: $KnowledgeGraphId, id: $id}}) RETURN {NodeReturn}",
            new { KnowledgeGraphId, id = nodeId },
            cancellationToken);
        return records.Count == 0 ? null : MapNode(records[0]);
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<KnowledgeGraphNodeRecord> Items, long Total)> ListNodesAsync(long KnowledgeGraphId, long? entityTypeId, string? keyword, int pageNo, int pageSize, CancellationToken cancellationToken)
    {
        const string where = "WHERE ($entityTypeId IS NULL OR n.entityTypeId = $entityTypeId) AND ($keyword IS NULL OR toLower(n.name) CONTAINS toLower($keyword))";
        var parameters = new { KnowledgeGraphId, entityTypeId, keyword = string.IsNullOrWhiteSpace(keyword) ? null : keyword, skip = (pageNo - 1) * pageSize, limit = pageSize };

        var countRecords = await ReadAsync($"MATCH (n:KgNode {{KnowledgeGraphId: $KnowledgeGraphId}}) {where} RETURN count(n) AS c", parameters, cancellationToken);
        var total = countRecords[0]["c"].As<long>();

        var records = await ReadAsync(
            $"MATCH (n:KgNode {{KnowledgeGraphId: $KnowledgeGraphId}}) {where} RETURN {NodeReturn} ORDER BY n.name SKIP $skip LIMIT $limit",
            parameters,
            cancellationToken);
        return (records.Select(MapNode).ToList(), total);
    }

    /// <inheritdoc/>
    public async Task<KnowledgeGraphEdgeRecord> CreateEdgeAsync(long KnowledgeGraphId, long relationTypeId, string sourceNodeId, string targetNodeId, CancellationToken cancellationToken)
    {
        await _provider.EnsureInitializedAsync(cancellationToken);
        var id = Guid.CreateVersion7().ToString();
        var records = await WriteReadAsync(
            "MATCH (s:KgNode {KnowledgeGraphId: $KnowledgeGraphId, id: $sourceNodeId}) " +
            "MATCH (t:KgNode {KnowledgeGraphId: $KnowledgeGraphId, id: $targetNodeId}) " +
            "CREATE (s)-[r:KG_REL {id: $id, KnowledgeGraphId: $KnowledgeGraphId, relationTypeId: $relationTypeId}]->(t) " +
            "RETURN r.id AS id",
            new { id, KnowledgeGraphId, relationTypeId, sourceNodeId, targetNodeId },
            cancellationToken);
        if (records.Count == 0)
        {
            throw new BusinessException("起点或终点节点不存在.") { StatusCode = 400 };
        }

        return new KnowledgeGraphEdgeRecord(id, KnowledgeGraphId, relationTypeId, sourceNodeId, targetNodeId);
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateEdgeAsync(long KnowledgeGraphId, string edgeId, long relationTypeId, CancellationToken cancellationToken)
    {
        var records = await WriteReadAsync(
            "MATCH ()-[r:KG_REL {KnowledgeGraphId: $KnowledgeGraphId, id: $id}]->() SET r.relationTypeId = $relationTypeId RETURN r.id AS id",
            new { KnowledgeGraphId, id = edgeId, relationTypeId },
            cancellationToken);
        return records.Count > 0;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteEdgeAsync(long KnowledgeGraphId, string edgeId, CancellationToken cancellationToken)
    {
        var records = await WriteReadAsync(
            "MATCH ()-[r:KG_REL {KnowledgeGraphId: $KnowledgeGraphId, id: $id}]->() DELETE r RETURN count(*) AS c",
            new { KnowledgeGraphId, id = edgeId },
            cancellationToken);
        return records.Count > 0 && records[0]["c"].As<long>() > 0;
    }

    /// <inheritdoc/>
    public async Task<KnowledgeGraphEdgeRecord?> GetEdgeAsync(long KnowledgeGraphId, string edgeId, CancellationToken cancellationToken)
    {
        var records = await ReadAsync(
            $"MATCH (s:KgNode {{KnowledgeGraphId: $KnowledgeGraphId}})-[r:KG_REL {{KnowledgeGraphId: $KnowledgeGraphId, id: $id}}]->(t:KgNode {{KnowledgeGraphId: $KnowledgeGraphId}}) RETURN {EdgeReturn}",
            new { KnowledgeGraphId, id = edgeId },
            cancellationToken);
        return records.Count == 0 ? null : MapEdge(records[0]);
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<KnowledgeGraphEdgeRecord> Items, long Total)> ListEdgesAsync(long KnowledgeGraphId, long? relationTypeId, string? nodeId, int pageNo, int pageSize, CancellationToken cancellationToken)
    {
        const string where = "WHERE ($relationTypeId IS NULL OR r.relationTypeId = $relationTypeId) AND ($nodeId IS NULL OR s.id = $nodeId OR t.id = $nodeId)";
        var parameters = new { KnowledgeGraphId, relationTypeId, nodeId = string.IsNullOrWhiteSpace(nodeId) ? null : nodeId, skip = (pageNo - 1) * pageSize, limit = pageSize };

        var countRecords = await ReadAsync($"MATCH (s:KgNode {{KnowledgeGraphId: $KnowledgeGraphId}})-[r:KG_REL {{KnowledgeGraphId: $KnowledgeGraphId}}]->(t:KgNode {{KnowledgeGraphId: $KnowledgeGraphId}}) {where} RETURN count(r) AS c", parameters, cancellationToken);
        var total = countRecords[0]["c"].As<long>();

        var records = await ReadAsync(
            $"MATCH (s:KgNode {{KnowledgeGraphId: $KnowledgeGraphId}})-[r:KG_REL {{KnowledgeGraphId: $KnowledgeGraphId}}]->(t:KgNode {{KnowledgeGraphId: $KnowledgeGraphId}}) {where} RETURN {EdgeReturn} ORDER BY r.id SKIP $skip LIMIT $limit",
            parameters,
            cancellationToken);
        return (records.Select(MapEdge).ToList(), total);
    }

    /// <inheritdoc/>
    public async Task PurgeGraphAsync(long KnowledgeGraphId, CancellationToken cancellationToken)
    {
        await WriteAsync("MATCH (n:KgNode {KnowledgeGraphId: $KnowledgeGraphId}) DETACH DELETE n", new { KnowledgeGraphId }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> ProbeDatabaseAsync(string database, CancellationToken cancellationToken)
    {
        try
        {
            var driver = await _provider.GetDriverAsync(cancellationToken);
            await using var session = driver.AsyncSession(b => b.WithDatabase(database));
            var cursor = await session.RunAsync("CALL db.labels()");
            await cursor.ConsumeAsync();
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex) when (ex is ServiceUnavailableException || ex is AuthenticationException)
        {
            throw new BusinessException("无法连接 Neo4j，请检查系统设置中的连接配置。") { StatusCode = 503 };
        }
        catch (Neo4jException)
        {
            // 数据库不存在等客户端错误，交由上层返回 400
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<KnowledgeGraphIntrospection> IntrospectAsync(string database, CancellationToken cancellationToken)
    {
        var driver = await _provider.GetDriverAsync(cancellationToken);
        await using var session = driver.AsyncSession(b => b.WithDatabase(database));

        var labelCursor = await session.RunAsync("CALL db.labels() YIELD label RETURN label ORDER BY label");
        var labels = new List<KnowledgeGraphIntrospectedItem>();
        foreach (var record in await labelCursor.ToListAsync())
        {
            var label = record["label"].As<string>();
            if (label.Contains('`', StringComparison.Ordinal))
            {
                continue;
            }

            long count;
            try
            {
                var countCursor = await session.RunAsync($"MATCH (n:`{label}`) RETURN count(n) AS c");
                var countRecord = (await countCursor.ToListAsync())[0];
                count = countRecord["c"].As<long>();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is not MoAI.Infra.Exceptions.BusinessException)
            {
                count = 0;
            }

            labels.Add(new KnowledgeGraphIntrospectedItem(label, count));
        }

        var relCursor = await session.RunAsync("CALL db.relationshipTypes() YIELD relationshipType RETURN relationshipType ORDER BY relationshipType");
        var relations = new List<KnowledgeGraphIntrospectedItem>();
        foreach (var record in await relCursor.ToListAsync())
        {
            var relType = record["relationshipType"].As<string>();
            if (relType.Contains('`', StringComparison.Ordinal))
            {
                continue;
            }

            long count;
            try
            {
                var countCursor = await session.RunAsync($"MATCH ()-[r:`{relType}`]->() RETURN count(r) AS c");
                var countRecord = (await countCursor.ToListAsync())[0];
                count = countRecord["c"].As<long>();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is not MoAI.Infra.Exceptions.BusinessException)
            {
                count = 0;
            }

            relations.Add(new KnowledgeGraphIntrospectedItem(relType, count));
        }

        var keyCursor = await session.RunAsync("CALL db.propertyKeys() YIELD propertyKey RETURN propertyKey ORDER BY propertyKey");
        var keys = (await keyCursor.ToListAsync()).Select(x => x["propertyKey"].As<string>()).ToList();

        return new KnowledgeGraphIntrospection(labels, relations, keys);
    }

    private static KnowledgeGraphNodeRecord MapNode(IRecord record)
        => new(record["id"].As<string>(), record["KnowledgeGraphId"].As<long>(), record["entityTypeId"].As<long>(), record["name"].As<string>(), record["description"].As<string>() ?? string.Empty);

    private static KnowledgeGraphEdgeRecord MapEdge(IRecord record)
        => new(record["id"].As<string>(), record["KnowledgeGraphId"].As<long>(), record["relationTypeId"].As<long>(), record["sourceNodeId"].As<string>(), record["targetNodeId"].As<string>());

    private async Task<List<IRecord>> ReadAsync(string cypher, object parameters, CancellationToken cancellationToken)
    {
        try
        {
            var driver = await _provider.GetDriverAsync(cancellationToken);
            await using var session = driver.AsyncSession();
            return await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, parameters);
                return await cursor.ToListAsync();
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex) when (ex is ServiceUnavailableException || (ex is Neo4jException and not ClientException))
        {
            throw new BusinessException("无法连接 Neo4j，请检查系统设置中的连接配置。") { StatusCode = 503 };
        }
    }

    private async Task<List<IRecord>> WriteReadAsync(string cypher, object parameters, CancellationToken cancellationToken)
    {
        try
        {
            var driver = await _provider.GetDriverAsync(cancellationToken);
            await using var session = driver.AsyncSession();
            var records = new List<IRecord>();
            await session.ExecuteWriteAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, parameters);
                records.AddRange(await cursor.ToListAsync());
            });
            return records;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex) when (ex is ServiceUnavailableException || (ex is Neo4jException and not ClientException))
        {
            throw new BusinessException("无法连接 Neo4j，请检查系统设置中的连接配置。") { StatusCode = 503 };
        }
    }

    private async Task WriteAsync(string cypher, object parameters, CancellationToken cancellationToken)
    {
        try
        {
            var driver = await _provider.GetDriverAsync(cancellationToken);
            await using var session = driver.AsyncSession();
            await session.ExecuteWriteAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, parameters);
                await cursor.ConsumeAsync();
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex) when (ex is ServiceUnavailableException || (ex is Neo4jException and not ClientException))
        {
            throw new BusinessException("无法连接 Neo4j，请检查系统设置中的连接配置。") { StatusCode = 503 };
        }
    }
}
