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
    private const string NodeReturn = "n.id AS id, n.kgId AS kgId, n.entityTypeId AS entityTypeId, n.name AS name, n.description AS description, n.propsJson AS propsJson";
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
    public async Task<KnowledgeGraphNodeRecord> CreateNodeAsync(long KnowledgeGraphId, long entityTypeId, string name, string description, string? propsJson, string? key, CancellationToken cancellationToken)
    {
        await _provider.EnsureInitializedAsync(cancellationToken);
        var id = Guid.CreateVersion7().ToString();
        await WriteAsync(
            "CREATE (n:KgNode {id: $id, kgId: $kgId, entityTypeId: $entityTypeId, name: $name, description: $description, propsJson: $propsJson, key: $key})",
            new { id, kgId = KnowledgeGraphId, entityTypeId, name, description, propsJson = propsJson ?? string.Empty, key = string.IsNullOrWhiteSpace(key) ? null : key },
            cancellationToken);
        return new KnowledgeGraphNodeRecord(id, KnowledgeGraphId, entityTypeId, name, description, propsJson);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<KnowledgeGraphNodeRecord>> CreateNodesBatchAsync(long KnowledgeGraphId, IReadOnlyList<KnowledgeGraphNodeInput> nodes, CancellationToken cancellationToken)
    {
        if (nodes.Count == 0)
        {
            return Array.Empty<KnowledgeGraphNodeRecord>();
        }

        await _provider.EnsureInitializedAsync(cancellationToken);
        var items = nodes.Select((node, index) => new
        {
            idx = (long)index,
            id = Guid.CreateVersion7().ToString(),
            entityTypeId = node.EntityTypeId,
            name = node.Name,
            description = node.Description,
            propsJson = node.PropsJson ?? string.Empty,
            key = string.IsNullOrWhiteSpace(node.Key) ? null : node.Key,
        }).ToList();

        // 单语句 UNWIND + CREATE 在单事务内执行，RETURN item.idx 保证返回行与输入序号对应；key 为 null 时属性不落库.
        var records = await WriteReadAsync(
            "UNWIND $items AS item " +
            "CREATE (n:KgNode {id: item.id, kgId: $kgId, entityTypeId: item.entityTypeId, name: item.name, description: item.description, propsJson: item.propsJson, key: item.key}) " +
            "RETURN item.idx AS idx, n.id AS id",
            new { kgId = KnowledgeGraphId, items },
            cancellationToken);
        var idByIdx = records.ToDictionary(x => x["idx"].As<long>(), x => x["id"].As<string>());

        return nodes.Select((node, index) => new KnowledgeGraphNodeRecord(
            idByIdx[index],
            KnowledgeGraphId,
            node.EntityTypeId,
            node.Name,
            node.Description,
            null)).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<KnowledgeGraphEdgeRecord>> CreateEdgesBatchAsync(long KnowledgeGraphId, IReadOnlyList<KnowledgeGraphEdgeInput> edges, CancellationToken cancellationToken)
    {
        if (edges.Count == 0)
        {
            return Array.Empty<KnowledgeGraphEdgeRecord>();
        }

        await _provider.EnsureInitializedAsync(cancellationToken);
        var items = edges.Select((edge, index) => new
        {
            idx = (long)index,
            id = Guid.CreateVersion7().ToString(),
            relationTypeId = edge.RelationTypeId,
            sourceNodeId = edge.SourceNodeId,
            targetNodeId = edge.TargetNodeId,
        }).ToList();

        // 单语句 UNWIND + MATCH + CREATE 在单事务内执行，RETURN item.idx 保证返回行与输入序号对应；
        // 行数断言在事务内进行，MATCH 不到的行（并发删除端点）触发回滚.
        var records = await WriteReadAsync(
            "UNWIND $items AS item " +
            "MATCH (s:KgNode {kgId: $kgId, id: item.sourceNodeId}) " +
            "MATCH (t:KgNode {kgId: $kgId, id: item.targetNodeId}) " +
            "CREATE (s)-[r:KG_REL {id: item.id, kgId: $kgId, relationTypeId: item.relationTypeId}]->(t) " +
            "RETURN item.idx AS idx, r.id AS id",
            new { kgId = KnowledgeGraphId, items },
            cancellationToken,
            assert: rows =>
            {
                if (rows.Count != edges.Count)
                {
                    throw new BusinessException("部分边的起点或终点节点不存在.") { StatusCode = 400 };
                }
            });

        var idByIdx = records.ToDictionary(x => x["idx"].As<long>(), x => x["id"].As<string>());
        return edges.Select((edge, index) => new KnowledgeGraphEdgeRecord(
            idByIdx[index],
            KnowledgeGraphId,
            edge.RelationTypeId,
            edge.SourceNodeId,
            edge.TargetNodeId)).ToList();
    }

    /// <inheritdoc/>
    public async Task UpdateNodeAsync(long KnowledgeGraphId, string nodeId, long entityTypeId, string name, string description, string? propsJson, string? key, CancellationToken cancellationToken)
    {
        await WriteAsync(
            "MATCH (n:KgNode {kgId: $kgId, id: $id}) SET n.entityTypeId = $entityTypeId, n.name = $name, n.description = $description, n.propsJson = $propsJson, " +
            "n.key = CASE WHEN $key IS NULL THEN n.key ELSE $key END",
            new { kgId = KnowledgeGraphId, id = nodeId, entityTypeId, name, description, propsJson = propsJson ?? string.Empty, key = string.IsNullOrWhiteSpace(key) ? null : key },
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
    public async Task<IReadOnlyList<string>> GetNodeIdsAsync(long KnowledgeGraphId, CancellationToken cancellationToken)
    {
        // 一期全量重嵌上限 5000 节点，超出截断
        var records = await ReadAsync(
            "MATCH (n:KgNode {kgId: $kgId}) RETURN n.id AS id ORDER BY n.id LIMIT $limit",
            new { kgId = KnowledgeGraphId, limit = 5000 },
            cancellationToken);
        return records.Select(x => x["id"].As<string>()).ToList();
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, long>> GetNodeTypesByIdsAsync(long KnowledgeGraphId, IReadOnlyList<string> nodeIds, CancellationToken cancellationToken)
    {
        if (nodeIds.Count == 0)
        {
            return new Dictionary<string, long>(StringComparer.Ordinal);
        }

        var records = await ReadAsync(
            "MATCH (n:KgNode {kgId: $kgId}) WHERE n.id IN $ids RETURN n.id AS id, n.entityTypeId AS entityTypeId",
            new { kgId = KnowledgeGraphId, ids = nodeIds },
            cancellationToken);
        return records.ToDictionary(x => x["id"].As<string>(), x => x["entityTypeId"].As<long>(), StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, KnowledgeGraphNodeRecord>> GetNodesByKeysAsync(long KnowledgeGraphId, IReadOnlyList<string> keys, CancellationToken cancellationToken)
    {
        if (keys.Count == 0)
        {
            return new Dictionary<string, KnowledgeGraphNodeRecord>(StringComparer.Ordinal);
        }

        var records = await ReadAsync(
            $"UNWIND $keys AS k MATCH (n:KgNode {{kgId: $kgId, key: k}}) RETURN k AS queryKey, {NodeReturn} ORDER BY n.id",
            new { kgId = KnowledgeGraphId, keys },
            cancellationToken);

        // 同一 key 理论唯一（导入 upsert 收敛）；历史脏数据出现重复时取首个，保持确定性
        var result = new Dictionary<string, KnowledgeGraphNodeRecord>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            var queryKey = record["queryKey"].As<string>();
            if (!result.ContainsKey(queryKey))
            {
                result[queryKey] = MapNode(record);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<Dictionary<(long EntityTypeId, string Name), KnowledgeGraphNodeRecord>> GetNodesByTypeAndNamesAsync(long KnowledgeGraphId, IReadOnlyList<(long EntityTypeId, string Name)> pairs, CancellationToken cancellationToken)
    {
        if (pairs.Count == 0)
        {
            return new Dictionary<(long, string), KnowledgeGraphNodeRecord>();
        }

        var items = pairs.Select(x => new { t = x.EntityTypeId, n = x.Name }).ToList();
        var records = await ReadAsync(
            $"UNWIND $items AS item MATCH (n:KgNode {{kgId: $kgId, entityTypeId: item.t, name: item.n}}) RETURN item.t AS pairType, item.n AS pairName, {NodeReturn} ORDER BY n.id",
            new { kgId = KnowledgeGraphId, items },
            cancellationToken);

        // 图谱不强制（类型,名称）唯一：重复时取首个（ORDER BY n.id 语义），保持确定性
        var result = new Dictionary<(long, string), KnowledgeGraphNodeRecord>();
        foreach (var record in records)
        {
            var key = (record["pairType"].As<long>(), record["pairName"].As<string>());
            if (!result.ContainsKey(key))
            {
                result[key] = MapNode(record);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<KnowledgeGraphNodeRecord>> GetNodesByNamesAsync(long KnowledgeGraphId, IReadOnlyList<string> names, CancellationToken cancellationToken)
    {
        if (names.Count == 0)
        {
            return Array.Empty<KnowledgeGraphNodeRecord>();
        }

        var records = await ReadAsync(
            $"UNWIND $names AS nm MATCH (n:KgNode {{kgId: $kgId, name: nm}}) RETURN {NodeReturn}",
            new { kgId = KnowledgeGraphId, names },
            cancellationToken);
        return records.Select(MapNode).ToList();
    }

    /// <inheritdoc/>
    public async Task<KnowledgeGraphNodeRecord?> GetNodeByKeyAsync(long KnowledgeGraphId, string key, CancellationToken cancellationToken)
    {
        var records = await ReadAsync(
            $"MATCH (n:KgNode {{kgId: $kgId, key: $key}}) RETURN {NodeReturn} LIMIT 1",
            new { kgId = KnowledgeGraphId, key },
            cancellationToken);
        return records.Count == 0 ? null : MapNode(records[0]);
    }

    /// <inheritdoc/>
    public async Task UpdateNodesBatchAsync(long KnowledgeGraphId, IReadOnlyList<KnowledgeGraphNodeUpdateInput> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        await _provider.EnsureInitializedAsync(cancellationToken);
        var parameters = items.Select(x => new
        {
            id = x.Id,
            entityTypeId = x.EntityTypeId,
            name = x.Name,
            description = x.Description,
            propsJson = x.PropsJson ?? string.Empty,
            key = string.IsNullOrWhiteSpace(x.Key) ? null : x.Key,
        }).ToList();

        // key 为 null 表示保留现有 key（仅按名称匹配收养 key 时才携带非 null）
        await WriteAsync(
            "UNWIND $items AS item " +
            "MATCH (n:KgNode {kgId: $kgId, id: item.id}) " +
            "SET n.entityTypeId = item.entityTypeId, n.name = item.name, n.description = item.description, n.propsJson = item.propsJson, " +
            "n.key = CASE WHEN item.key IS NULL THEN n.key ELSE item.key END",
            new { kgId = KnowledgeGraphId, items = parameters },
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<HashSet<string>> GetExistingEdgeKeysAsync(long KnowledgeGraphId, IReadOnlyList<(string SourceNodeId, long RelationTypeId, string TargetNodeId)> triples, CancellationToken cancellationToken)
    {
        if (triples.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var items = triples.Select(x => new { s = x.SourceNodeId, rt = x.RelationTypeId, t = x.TargetNodeId }).ToList();
        var records = await ReadAsync(
            "UNWIND $items AS item " +
            "MATCH (s:KgNode {kgId: $kgId, id: item.s})-[r:KG_REL {kgId: $kgId, relationTypeId: item.rt}]->(t:KgNode {kgId: $kgId, id: item.t}) " +
            "RETURN item.s AS s, item.rt AS rt, item.t AS t",
            new { kgId = KnowledgeGraphId, items },
            cancellationToken);

        // 同一三元组可能有多条平行边：去重为复合键集合
        return records.Select(x => $"{x["s"].As<string>()}|{x["rt"].As<long>()}|{x["t"].As<string>()}").ToHashSet(StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> DeleteNodesByKeysAsync(long KnowledgeGraphId, IReadOnlyList<string> keys, CancellationToken cancellationToken)
    {
        if (keys.Count == 0)
        {
            return Array.Empty<string>();
        }

        await _provider.EnsureInitializedAsync(cancellationToken);

        // 先读命中节点 id（供向量删除增量），再统一删除；删除后不可再访问节点属性（memgraph 方言会报错），故两步执行
        var idRecords = await ReadAsync(
            "UNWIND $keys AS k MATCH (n:KgNode {kgId: $kgId, key: k}) RETURN n.id AS id",
            new { kgId = KnowledgeGraphId, keys },
            cancellationToken);
        var ids = idRecords.Select(x => x["id"].As<string>()).ToList();
        if (ids.Count == 0)
        {
            return ids;
        }

        await WriteAsync(
            "UNWIND $ids AS id MATCH (n:KgNode {kgId: $kgId, id: id}) DETACH DELETE n",
            new { kgId = KnowledgeGraphId, ids },
            cancellationToken);
        return ids;
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<KnowledgeGraphNodeKeyRecord> Items, long Total)> ListNodeKeysAsync(long KnowledgeGraphId, int pageNo, int pageSize, CancellationToken cancellationToken)
    {
        var countRecords = await ReadAsync(
            "MATCH (n:KgNode {kgId: $kgId}) WHERE n.key IS NOT NULL RETURN count(n) AS c",
            new { kgId = KnowledgeGraphId },
            cancellationToken);
        var total = countRecords[0]["c"].As<long>();

        var records = await ReadAsync(
            "MATCH (n:KgNode {kgId: $kgId}) WHERE n.key IS NOT NULL RETURN n.key AS key, n.id AS id, n.name AS name, n.entityTypeId AS entityTypeId ORDER BY n.key SKIP $skip LIMIT $limit",
            new { kgId = KnowledgeGraphId, skip = (pageNo - 1) * pageSize, limit = pageSize },
            cancellationToken);
        var items = records
            .Select(x => new KnowledgeGraphNodeKeyRecord(x["key"].As<string>(), x["id"].As<string>(), x["name"].As<string>(), x["entityTypeId"].As<long>()))
            .ToList();
        return (items, total);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> DeleteEdgesByTriplesAsync(long KnowledgeGraphId, IReadOnlyList<(string SourceNodeId, long RelationTypeId, string TargetNodeId)> triples, CancellationToken cancellationToken)
    {
        var distinct = triples
            .Distinct()
            .Select(x => new { s = x.SourceNodeId, rt = x.RelationTypeId, t = x.TargetNodeId })
            .ToList();
        if (distinct.Count == 0)
        {
            return Array.Empty<string>();
        }

        // 先读命中边 id（同三元组平行边全收），再统一删除；删除后不可再访问边属性（memgraph 方言会报错），故两步执行
        var idRecords = await ReadAsync(
            "UNWIND $items AS item " +
            "MATCH (s:KgNode {kgId: $kgId, id: item.s})-[r:KG_REL {kgId: $kgId, relationTypeId: item.rt}]->(t:KgNode {kgId: $kgId, id: item.t}) " +
            "RETURN DISTINCT r.id AS id",
            new { kgId = KnowledgeGraphId, items = distinct },
            cancellationToken);
        var ids = idRecords.Select(x => x["id"].As<string>()).ToList();
        if (ids.Count == 0)
        {
            return ids;
        }

        await WriteAsync(
            "UNWIND $ids AS eid MATCH ()-[r:KG_REL {kgId: $kgId}]->() WHERE r.id = eid DELETE r",
            new { kgId = KnowledgeGraphId, ids },
            cancellationToken);
        return ids;
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

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<KnowledgeGraphConnectedNodeRecord> Nodes, IReadOnlyList<KnowledgeGraphConnectedEdgeRecord> Edges, bool Truncated)> QueryConnectedCanvasAsync(string database, string? label, string? keyword, int limit, CancellationToken cancellationToken)
    {
        // 外部节点无统一 schema：elementId 定位、name/title/id 属性启发式取名、首个标签做实体类型.
        var nameExpr = "coalesce(toString(n.name), toString(n.title), toString(n.id), elementId(n))";
        var match = string.IsNullOrWhiteSpace(label) ? "MATCH (n) " : $"MATCH (n:`{EscapeIdentifier(label)}`) ";
        var nodeCypher =
            $"{match}WHERE ($keyword IS NULL OR toLower({nameExpr}) CONTAINS toLower($keyword)) " +
            $"RETURN elementId(n) AS id, labels(n) AS labels, {nameExpr} AS name, coalesce(toString(n.description), '') AS description " +
            "ORDER BY name, id LIMIT $limitPlus1";

        var nodeRecords = await ReadDatabaseAsync(database, nodeCypher, new { keyword = string.IsNullOrWhiteSpace(keyword) ? null : keyword, limitPlus1 = limit + 1 }, cancellationToken);
        var truncated = nodeRecords.Count > limit;
        var nodes = nodeRecords.Take(limit).Select(MapConnectedNode).ToList();
        if (nodes.Count == 0)
        {
            return (nodes, Array.Empty<KnowledgeGraphConnectedEdgeRecord>(), truncated);
        }

        var ids = nodes.Select(x => x.Id).ToList();
        var edgeCypher =
            "MATCH (s)-[r]->(t) " +
            "WHERE elementId(s) IN $ids AND elementId(t) IN $ids " +
            "RETURN elementId(r) AS id, type(r) AS relationType, elementId(s) AS sourceNodeId, elementId(t) AS targetNodeId " +
            "ORDER BY id LIMIT $edgeLimit";
        var edgeRecords = await ReadDatabaseAsync(database, edgeCypher, new { ids, edgeLimit = Math.Min(limit * 5, 5000) }, cancellationToken);
        var edges = edgeRecords.Select(MapConnectedEdge).ToList();
        return (nodes, edges, truncated);
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<KnowledgeGraphConnectedNodeRecord> Nodes, IReadOnlyList<KnowledgeGraphConnectedEdgeRecord> Edges, bool Truncated)> GetConnectedNeighborsAsync(string database, string nodeId, int limit, CancellationToken cancellationToken)
    {
        var nameExpr = "coalesce(toString(n.name), toString(n.title), toString(n.id), elementId(n))";
        var cypher =
            "MATCH (n) WHERE elementId(n) = $nodeId " +
            "MATCH (n)-[r]-(m) " +
            $"RETURN elementId(m) AS id, labels(m) AS labels, {nameExpr} AS name, coalesce(toString(m.description), '') AS description, " +
            "elementId(r) AS edgeId, type(r) AS relationType, elementId(startNode(r)) AS sourceNodeId, elementId(endNode(r)) AS targetNodeId " +
            "ORDER BY name, id LIMIT $limitPlus1";
        var records = await ReadDatabaseAsync(database, cypher, new { nodeId, limitPlus1 = limit + 1 }, cancellationToken);

        var truncated = records.Count > limit;
        var nodeMap = new Dictionary<string, KnowledgeGraphConnectedNodeRecord>(StringComparer.Ordinal);
        var edgeMap = new Dictionary<string, KnowledgeGraphConnectedEdgeRecord>(StringComparer.Ordinal);
        foreach (var record in records.Take(limit))
        {
            var connectedNode = MapConnectedNode(record);
            nodeMap[connectedNode.Id] = connectedNode;
            edgeMap[record["edgeId"].As<string>()] = MapConnectedEdge(record);
        }

        return (nodeMap.Values.ToList(), edgeMap.Values.ToList(), truncated);
    }

    /// <inheritdoc/>
    public async Task<KnowledgeGraphConnectedNodeRecord?> GetConnectedNodeAsync(string database, string nodeId, CancellationToken cancellationToken)
    {
        var nameExpr = "coalesce(toString(n.name), toString(n.title), toString(n.id), elementId(n))";
        var cypher =
            "MATCH (n) WHERE elementId(n) = $nodeId " +
            $"RETURN elementId(n) AS id, labels(n) AS labels, {nameExpr} AS name, coalesce(toString(n.description), '') AS description " +
            "LIMIT 1";
        var records = await ReadDatabaseAsync(database, cypher, new { nodeId }, cancellationToken);
        return records.Count == 0 ? null : MapConnectedNode(records[0]);
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
        => new(
            record["id"].As<string>(),
            record["kgId"].As<long>(),
            record["entityTypeId"].As<long>(),
            record["name"].As<string>(),
            record["description"].As<string>() ?? string.Empty,
            record["propsJson"] is { } pj ? pj.As<string>() : null);

    private static KnowledgeGraphConnectedNodeRecord MapConnectedNode(IRecord record)
    {
        var label = NormalizeLabelValues(record["labels"]).FirstOrDefault() ?? string.Empty;
        return new KnowledgeGraphConnectedNodeRecord(
            record["id"].As<string>(),
            label,
            record["name"].As<string>(),
            record["description"].As<string>() ?? string.Empty);
    }

    private static KnowledgeGraphConnectedEdgeRecord MapConnectedEdge(IRecord record)
        => new(record["id"].As<string>(), record["relationType"].As<string>(), record["sourceNodeId"].As<string>(), record["targetNodeId"].As<string>());

    /// <summary>
    /// 反引号转义：label 来自内省结果（含反引号的已被内省过滤），此处兜底拒绝，防 Cypher 注入.
    /// </summary>
    private static string EscapeIdentifier(string label)
    {
        if (label.Contains('`', StringComparison.Ordinal))
        {
            throw new BusinessException("标签名包含非法字符.") { StatusCode = 400 };
        }

        return label;
    }

    /// <summary>
    /// 按数据库名路由的读取（外部接入库），错误语义与默认库读取一致.
    /// </summary>
    private async Task<List<IRecord>> ReadDatabaseAsync(string database, string cypher, object parameters, CancellationToken cancellationToken)
    {
        try
        {
            var (driver, dialect) = await _provider.GetRuntimeAsync(cancellationToken);
            await using var session = OpenSession(driver, database, dialect);
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

    private async Task<List<IRecord>> WriteReadAsync(string cypher, object parameters, CancellationToken cancellationToken, Action<List<IRecord>>? assert = null)
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
                // 断言在事务内执行：抛出异常即回滚，避免并发写导致的局部提交.
                assert?.Invoke(records);
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
