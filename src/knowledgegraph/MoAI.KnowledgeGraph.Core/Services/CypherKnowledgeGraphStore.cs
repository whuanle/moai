using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Models;
using MoAI.Settings.Models;
using Neo4j.Driver;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 基于 openCypher 的知识图谱存储（方言：memgraph / neo4j，Bolt 协议经 Neo4j.Driver 访问）.
/// </summary>
public sealed class CypherKnowledgeGraphStore : IKnowledgeGraphStore
{
    private const string NodeReturn = "n.id AS id, n.kgId AS kgId, n.entityTypeId AS entityTypeId, n.name AS name, n.description AS description";
    private const string EdgeReturn = "r.id AS id, r.kgId AS kgId, r.relationTypeId AS relationTypeId, s.id AS sourceNodeId, t.id AS targetNodeId";

    private readonly GraphDriverProvider _provider;

    /// <summary>
    /// Initializes a new instance of the <see cref="CypherKnowledgeGraphStore"/> class.
    /// </summary>
    /// <param name="provider">图数据库驱动提供者.</param>
    public CypherKnowledgeGraphStore(GraphDriverProvider provider)
    {
        _provider = provider;
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<KnowledgeGraphNodeRecord> Nodes, IReadOnlyList<KnowledgeGraphEdgeRecord> Edges, bool Truncated)> QueryCanvasAsync(long KnowledgeGraphId, long? entityTypeId, long? relationTypeId, string? keyword, int limit, CancellationToken cancellationToken)
    {
        var nodeCypher =
            "MATCH (n:KgNode {kgId: $kgId}) " +
            "WHERE ($entityTypeId IS NULL OR n.entityTypeId = $entityTypeId) AND ($keyword IS NULL OR toLower(n.name) CONTAINS toLower($keyword)) " +
            $"RETURN {NodeReturn} ORDER BY n.name, n.id LIMIT $limitPlus1";
        var nodeParameters = new
        {
            kgId = KnowledgeGraphId,
            entityTypeId,
            keyword = string.IsNullOrWhiteSpace(keyword) ? null : keyword,
            limitPlus1 = limit + 1
        };

        var nodeRecords = await ReadAsync(nodeCypher, nodeParameters, cancellationToken);
        var truncated = nodeRecords.Count > limit;
        var nodes = nodeRecords.Take(limit).Select(MapNode).ToList();
        if (nodes.Count == 0)
        {
            return (nodes, Array.Empty<KnowledgeGraphEdgeRecord>(), truncated);
        }

        var ids = nodes.Select(x => x.Id).ToList();
        var edgeCypher =
            "MATCH (s:KgNode {kgId: $kgId})-[r:KG_REL {kgId: $kgId}]->(t:KgNode {kgId: $kgId}) " +
            "WHERE s.id IN $ids AND t.id IN $ids AND ($relationTypeId IS NULL OR r.relationTypeId = $relationTypeId) " +
            $"RETURN {EdgeReturn} ORDER BY r.id LIMIT $edgeLimit";
        var edgeRecords = await ReadAsync(edgeCypher, new { kgId = KnowledgeGraphId, ids, relationTypeId, edgeLimit = Math.Min(limit * 5, 5000) }, cancellationToken);
        var edges = edgeRecords.Select(MapEdge).ToList();
        return (nodes, edges, truncated);
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<KnowledgeGraphNodeRecord> Nodes, IReadOnlyList<KnowledgeGraphEdgeRecord> Edges, bool Truncated)> GetNeighborsAsync(long KnowledgeGraphId, string nodeId, int limit, CancellationToken cancellationToken)
    {
        var cypher =
            "MATCH (n:KgNode {kgId: $kgId, id: $nodeId})-[r:KG_REL {kgId: $kgId}]-(m:KgNode {kgId: $kgId}) " +
            "RETURN m.id AS id, m.kgId AS kgId, m.entityTypeId AS entityTypeId, m.name AS name, m.description AS description, " +
            "r.id AS edgeId, r.relationTypeId AS relationTypeId, startNode(r).id AS sourceNodeId, endNode(r).id AS targetNodeId " +
            "ORDER BY m.name, m.id LIMIT $limitPlus1";
        var records = await ReadAsync(cypher, new { kgId = KnowledgeGraphId, nodeId, limitPlus1 = limit + 1 }, cancellationToken);

        var truncated = records.Count > limit;
        var nodeMap = new Dictionary<string, KnowledgeGraphNodeRecord>(StringComparer.Ordinal);
        var edgeMap = new Dictionary<string, KnowledgeGraphEdgeRecord>(StringComparer.Ordinal);
        foreach (var record in records.Take(limit))
        {
            nodeMap[record["id"].As<string>()] = new KnowledgeGraphNodeRecord(
                record["id"].As<string>(),
                record["kgId"].As<long>(),
                record["entityTypeId"].As<long>(),
                record["name"].As<string>(),
                record["description"].As<string>() ?? string.Empty);
            edgeMap[record["edgeId"].As<string>()] = new KnowledgeGraphEdgeRecord(
                record["edgeId"].As<string>(),
                record["kgId"].As<long>(),
                record["relationTypeId"].As<long>(),
                record["sourceNodeId"].As<string>(),
                record["targetNodeId"].As<string>());
        }

        return (nodeMap.Values.ToList(), edgeMap.Values.ToList(), truncated);
    }

    /// <inheritdoc/>
    public async Task<int> CountNodesByEntityTypeAsync(long KnowledgeGraphId, long entityTypeId, CancellationToken cancellationToken)
    {
        var records = await ReadAsync(
            "MATCH (n:KgNode {kgId: $kgId, entityTypeId: $entityTypeId}) RETURN count(n) AS c",
            new { kgId = KnowledgeGraphId, entityTypeId },
            cancellationToken);
        return (int)records[0]["c"].As<long>();
    }

    /// <inheritdoc/>
    public async Task<int> CountEdgesByRelationTypeAsync(long KnowledgeGraphId, long relationTypeId, CancellationToken cancellationToken)
    {
        var records = await ReadAsync(
            "MATCH ()-[r:KG_REL {kgId: $kgId, relationTypeId: $relationTypeId}]->() RETURN count(r) AS c",
            new { kgId = KnowledgeGraphId, relationTypeId },
            cancellationToken);
        return (int)records[0]["c"].As<long>();
    }

    /// <inheritdoc/>
    public async Task<KnowledgeGraphNodeRecord> CreateNodeAsync(long KnowledgeGraphId, long entityTypeId, string name, string description, CancellationToken cancellationToken)
    {
        await _provider.EnsureInitializedAsync(cancellationToken);
        var id = Guid.CreateVersion7().ToString();
        await WriteAsync(
            "CREATE (n:KgNode {id: $id, kgId: $kgId, entityTypeId: $entityTypeId, name: $name, description: $description})",
            new { id, kgId = KnowledgeGraphId, entityTypeId, name, description },
            cancellationToken);
        return new KnowledgeGraphNodeRecord(id, KnowledgeGraphId, entityTypeId, name, description);
    }

    /// <inheritdoc/>
    public async Task UpdateNodeAsync(long KnowledgeGraphId, string nodeId, long entityTypeId, string name, string description, CancellationToken cancellationToken)
    {
        await WriteAsync(
            "MATCH (n:KgNode {kgId: $kgId, id: $id}) SET n.entityTypeId = $entityTypeId, n.name = $name, n.description = $description",
            new { kgId = KnowledgeGraphId, id = nodeId, entityTypeId, name, description },
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteNodeAsync(long KnowledgeGraphId, string nodeId, CancellationToken cancellationToken)
    {
        var records = await WriteReadAsync(
            "MATCH (n:KgNode {kgId: $kgId, id: $id}) DETACH DELETE n RETURN count(*) AS c",
            new { kgId = KnowledgeGraphId, id = nodeId },
            cancellationToken);
        return records.Count > 0 && records[0]["c"].As<long>() > 0;
    }

    /// <inheritdoc/>
    public async Task<KnowledgeGraphNodeRecord?> GetNodeAsync(long KnowledgeGraphId, string nodeId, CancellationToken cancellationToken)
    {
        var records = await ReadAsync(
            $"MATCH (n:KgNode {{kgId: $kgId, id: $id}}) RETURN {NodeReturn}",
            new { kgId = KnowledgeGraphId, id = nodeId },
            cancellationToken);
        return records.Count == 0 ? null : MapNode(records[0]);
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<KnowledgeGraphNodeRecord> Items, long Total)> ListNodesAsync(long KnowledgeGraphId, long? entityTypeId, string? keyword, int pageNo, int pageSize, CancellationToken cancellationToken)
    {
        const string where = "WHERE ($entityTypeId IS NULL OR n.entityTypeId = $entityTypeId) AND ($keyword IS NULL OR toLower(n.name) CONTAINS toLower($keyword))";
        var parameters = new { kgId = KnowledgeGraphId, entityTypeId, keyword = string.IsNullOrWhiteSpace(keyword) ? null : keyword, skip = (pageNo - 1) * pageSize, limit = pageSize };

        var countRecords = await ReadAsync($"MATCH (n:KgNode {{kgId: $kgId}}) {where} RETURN count(n) AS c", parameters, cancellationToken);
        var total = countRecords[0]["c"].As<long>();

        var records = await ReadAsync(
            $"MATCH (n:KgNode {{kgId: $kgId}}) {where} RETURN {NodeReturn} ORDER BY n.name SKIP $skip LIMIT $limit",
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
            "MATCH (s:KgNode {kgId: $kgId, id: $sourceNodeId}) " +
            "MATCH (t:KgNode {kgId: $kgId, id: $targetNodeId}) " +
            "CREATE (s)-[r:KG_REL {id: $id, kgId: $kgId, relationTypeId: $relationTypeId}]->(t) " +
            "RETURN r.id AS id",
            new { id, kgId = KnowledgeGraphId, relationTypeId, sourceNodeId, targetNodeId },
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
            "MATCH ()-[r:KG_REL {kgId: $kgId, id: $id}]->() SET r.relationTypeId = $relationTypeId RETURN r.id AS id",
            new { kgId = KnowledgeGraphId, id = edgeId, relationTypeId },
            cancellationToken);
        return records.Count > 0;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteEdgeAsync(long KnowledgeGraphId, string edgeId, CancellationToken cancellationToken)
    {
        var records = await WriteReadAsync(
            "MATCH ()-[r:KG_REL {kgId: $kgId, id: $id}]->() DELETE r RETURN count(*) AS c",
            new { kgId = KnowledgeGraphId, id = edgeId },
            cancellationToken);
        return records.Count > 0 && records[0]["c"].As<long>() > 0;
    }

    /// <inheritdoc/>
    public async Task<KnowledgeGraphEdgeRecord?> GetEdgeAsync(long KnowledgeGraphId, string edgeId, CancellationToken cancellationToken)
    {
        var records = await ReadAsync(
            $"MATCH (s:KgNode {{kgId: $kgId}})-[r:KG_REL {{kgId: $kgId, id: $id}}]->(t:KgNode {{kgId: $kgId}}) RETURN {EdgeReturn}",
            new { kgId = KnowledgeGraphId, id = edgeId },
            cancellationToken);
        return records.Count == 0 ? null : MapEdge(records[0]);
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<KnowledgeGraphEdgeRecord> Items, long Total)> ListEdgesAsync(long KnowledgeGraphId, long? relationTypeId, string? nodeId, int pageNo, int pageSize, CancellationToken cancellationToken)
    {
        const string where = "WHERE ($relationTypeId IS NULL OR r.relationTypeId = $relationTypeId) AND ($nodeId IS NULL OR s.id = $nodeId OR t.id = $nodeId)";
        var parameters = new { kgId = KnowledgeGraphId, relationTypeId, nodeId = string.IsNullOrWhiteSpace(nodeId) ? null : nodeId, skip = (pageNo - 1) * pageSize, limit = pageSize };

        var countRecords = await ReadAsync($"MATCH (s:KgNode {{kgId: $kgId}})-[r:KG_REL {{kgId: $kgId}}]->(t:KgNode {{kgId: $kgId}}) {where} RETURN count(r) AS c", parameters, cancellationToken);
        var total = countRecords[0]["c"].As<long>();

        var records = await ReadAsync(
            $"MATCH (s:KgNode {{kgId: $kgId}})-[r:KG_REL {{kgId: $kgId}}]->(t:KgNode {{kgId: $kgId}}) {where} RETURN {EdgeReturn} ORDER BY r.id SKIP $skip LIMIT $limit",
            parameters,
            cancellationToken);
        return (records.Select(MapEdge).ToList(), total);
    }

    /// <inheritdoc/>
    public async Task PurgeGraphAsync(long KnowledgeGraphId, CancellationToken cancellationToken)
    {
        await WriteAsync("MATCH (n:KgNode {kgId: $kgId}) DETACH DELETE n", new { kgId = KnowledgeGraphId }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> ProbeDatabaseAsync(string database, CancellationToken cancellationToken)
    {
        try
        {
            var (driver, dialect) = await _provider.GetRuntimeAsync(cancellationToken);
            await using var session = OpenSession(driver, database, dialect);
            var cursor = await session.RunAsync(ProbeCypher(dialect));
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
            throw new BusinessException("无法连接图数据库，请检查系统设置中的连接配置。") { StatusCode = 503 };
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
        var (driver, dialect) = await _provider.GetRuntimeAsync(cancellationToken);
        var isNeo4j = string.Equals(dialect, KnowledgeGraphStoreSettings.DialectNeo4j, StringComparison.OrdinalIgnoreCase);
        await using var session = OpenSession(driver, database, dialect);

        // Memgraph 3.x 移除了 mg.labels 等内省过程（SHOW SCHEMA INFO 需启动参数开启），
        // 且 labels(n) 对单标签节点返回字符串而非列表（UNWIND 会报错），
        // 故 memgraph 方言用数据派生查询内省（空库返回空列表）。
        var labelCypher = isNeo4j
            ? "CALL db.labels() YIELD label RETURN label ORDER BY label"
            : "MATCH (n) RETURN DISTINCT labels(n) AS labels";
        var labelCursor = await session.RunAsync(labelCypher);
        var labels = new List<KnowledgeGraphIntrospectedItem>();
        foreach (var record in await labelCursor.ToListAsync())
        {
            var labelValues = isNeo4j
                ? new[] { record["label"].As<string>() }
                : NormalizeLabelValues(record["labels"]);
            foreach (var label in labelValues)
            {
                if (string.IsNullOrEmpty(label) || label.Contains('`', StringComparison.Ordinal))
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
        }

        var relCypher = isNeo4j
            ? "CALL db.relationshipTypes() YIELD relationshipType RETURN relationshipType ORDER BY relationshipType"
            : "MATCH ()-[r]->() RETURN DISTINCT type(r) AS relationType";
        var relCursor = await session.RunAsync(relCypher);
        var relations = new List<KnowledgeGraphIntrospectedItem>();
        foreach (var record in await relCursor.ToListAsync())
        {
            var relType = record[isNeo4j ? "relationshipType" : "relationType"].As<string>();
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

        var keyCypher = isNeo4j
            ? "CALL db.propertyKeys() YIELD propertyKey RETURN propertyKey ORDER BY propertyKey"
            : "MATCH (n) UNWIND keys(n) AS propertyKey RETURN DISTINCT propertyKey";
        var keyCursor = await session.RunAsync(keyCypher);
        var keys = (await keyCursor.ToListAsync()).Select(x => x["propertyKey"].As<string>()).ToList();

        return new KnowledgeGraphIntrospection(labels, relations, keys);
    }

    private static string ProbeCypher(string dialect)
        => string.Equals(dialect, KnowledgeGraphStoreSettings.DialectNeo4j, StringComparison.OrdinalIgnoreCase)
            ? "CALL db.labels()"
            : "RETURN 1 AS ok";

    /// <summary>
    /// 归一化 labels(n) 结果：Memgraph 对单标签节点返回字符串，多标签返回列表.
    /// </summary>
    private static IEnumerable<string> NormalizeLabelValues(object? value)
    {
        if (value is string s)
        {
            return new[] { s };
        }

        if (value is IEnumerable<object> list)
        {
            return list.Select(x => x?.ToString() ?? string.Empty);
        }

        return Array.Empty<string>();
    }

    /// <summary>
    /// 打开会话；仅 neo4j 方言（外部接入可能使用多数据库）按库名路由，memgraph 恒用默认库.
    /// </summary>
    private static IAsyncSession OpenSession(IDriver driver, string? database, string dialect)
    {
        var useDatabase = string.Equals(dialect, KnowledgeGraphStoreSettings.DialectNeo4j, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(database);
        return useDatabase
            ? driver.AsyncSession(b => b.WithDatabase(database!))
            : driver.AsyncSession();
    }

    private static KnowledgeGraphNodeRecord MapNode(IRecord record)
        => new(record["id"].As<string>(), record["kgId"].As<long>(), record["entityTypeId"].As<long>(), record["name"].As<string>(), record["description"].As<string>() ?? string.Empty);

    private static KnowledgeGraphEdgeRecord MapEdge(IRecord record)
        => new(record["id"].As<string>(), record["kgId"].As<long>(), record["relationTypeId"].As<long>(), record["sourceNodeId"].As<string>(), record["targetNodeId"].As<string>());

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
            throw new BusinessException("无法连接图数据库，请检查系统设置中的连接配置。") { StatusCode = 503 };
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
            throw new BusinessException("无法连接图数据库，请检查系统设置中的连接配置。") { StatusCode = 503 };
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
            throw new BusinessException("无法连接图数据库，请检查系统设置中的连接配置。") { StatusCode = 503 };
        }
    }
}
