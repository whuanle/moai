using MoAI.Infra.Exceptions;
using Neo4j.Driver;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 基于 Neo4j 的知识图谱存储.
/// </summary>
public sealed class Neo4jKnowledgeGraphStore : IKnowledgeGraphStore
{
    private const string NodeReturn = "n.id AS id, n.kgId AS kgId, n.entityTypeId AS entityTypeId, n.name AS name, n.description AS description";
    private const string EdgeReturn = "r.id AS id, r.kgId AS kgId, r.relationTypeId AS relationTypeId, s.id AS sourceNodeId, t.id AS targetNodeId";

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
    public async Task<int> CountNodesByEntityTypeAsync(long kgId, long entityTypeId, CancellationToken cancellationToken)
    {
        var records = await ReadAsync(
            "MATCH (n:KgNode {kgId: $kgId, entityTypeId: $entityTypeId}) RETURN count(n) AS c",
            new { kgId, entityTypeId },
            cancellationToken);
        return (int)records[0]["c"].As<long>();
    }

    /// <inheritdoc/>
    public async Task<int> CountEdgesByRelationTypeAsync(long kgId, long relationTypeId, CancellationToken cancellationToken)
    {
        var records = await ReadAsync(
            "MATCH ()-[r:KG_REL {kgId: $kgId, relationTypeId: $relationTypeId}]->() RETURN count(r) AS c",
            new { kgId, relationTypeId },
            cancellationToken);
        return (int)records[0]["c"].As<long>();
    }

    /// <inheritdoc/>
    public async Task<KnowledgeGraphNodeRecord> CreateNodeAsync(long kgId, long entityTypeId, string name, string description, CancellationToken cancellationToken)
    {
        await _provider.EnsureInitializedAsync(cancellationToken);
        var id = Guid.CreateVersion7().ToString();
        await WriteAsync(
            "CREATE (n:KgNode {id: $id, kgId: $kgId, entityTypeId: $entityTypeId, name: $name, description: $description})",
            new { id, kgId, entityTypeId, name, description },
            cancellationToken);
        return new KnowledgeGraphNodeRecord(id, kgId, entityTypeId, name, description);
    }

    /// <inheritdoc/>
    public async Task UpdateNodeAsync(long kgId, string nodeId, long entityTypeId, string name, string description, CancellationToken cancellationToken)
    {
        await WriteAsync(
            "MATCH (n:KgNode {kgId: $kgId, id: $id}) SET n.entityTypeId = $entityTypeId, n.name = $name, n.description = $description",
            new { kgId, id = nodeId, entityTypeId, name, description },
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteNodeAsync(long kgId, string nodeId, CancellationToken cancellationToken)
    {
        var records = await WriteReadAsync(
            "MATCH (n:KgNode {kgId: $kgId, id: $id}) DETACH DELETE n RETURN count(*) AS c",
            new { kgId, id = nodeId },
            cancellationToken);
        return records.Count > 0 && records[0]["c"].As<long>() > 0;
    }

    /// <inheritdoc/>
    public async Task<KnowledgeGraphNodeRecord?> GetNodeAsync(long kgId, string nodeId, CancellationToken cancellationToken)
    {
        var records = await ReadAsync(
            $"MATCH (n:KgNode {{kgId: $kgId, id: $id}}) RETURN {NodeReturn}",
            new { kgId, id = nodeId },
            cancellationToken);
        return records.Count == 0 ? null : MapNode(records[0]);
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<KnowledgeGraphNodeRecord> Items, long Total)> ListNodesAsync(long kgId, long? entityTypeId, string? keyword, int pageNo, int pageSize, CancellationToken cancellationToken)
    {
        const string where = "WHERE ($entityTypeId IS NULL OR n.entityTypeId = $entityTypeId) AND ($keyword IS NULL OR toLower(n.name) CONTAINS toLower($keyword))";
        var parameters = new { kgId, entityTypeId, keyword = string.IsNullOrWhiteSpace(keyword) ? null : keyword, skip = (pageNo - 1) * pageSize, limit = pageSize };

        var countRecords = await ReadAsync($"MATCH (n:KgNode {{kgId: $kgId}}) {where} RETURN count(n) AS c", parameters, cancellationToken);
        var total = countRecords[0]["c"].As<long>();

        var records = await ReadAsync(
            $"MATCH (n:KgNode {{kgId: $kgId}}) {where} RETURN {NodeReturn} ORDER BY n.name SKIP $skip LIMIT $limit",
            parameters,
            cancellationToken);
        return (records.Select(MapNode).ToList(), total);
    }

    /// <inheritdoc/>
    public async Task<KnowledgeGraphEdgeRecord> CreateEdgeAsync(long kgId, long relationTypeId, string sourceNodeId, string targetNodeId, CancellationToken cancellationToken)
    {
        await _provider.EnsureInitializedAsync(cancellationToken);
        var id = Guid.CreateVersion7().ToString();
        var records = await WriteReadAsync(
            "MATCH (s:KgNode {kgId: $kgId, id: $sourceNodeId}) " +
            "MATCH (t:KgNode {kgId: $kgId, id: $targetNodeId}) " +
            "CREATE (s)-[r:KG_REL {id: $id, kgId: $kgId, relationTypeId: $relationTypeId}]->(t) " +
            "RETURN r.id AS id",
            new { id, kgId, relationTypeId, sourceNodeId, targetNodeId },
            cancellationToken);
        if (records.Count == 0)
        {
            throw new BusinessException("起点或终点节点不存在.") { StatusCode = 400 };
        }

        return new KnowledgeGraphEdgeRecord(id, kgId, relationTypeId, sourceNodeId, targetNodeId);
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateEdgeAsync(long kgId, string edgeId, long relationTypeId, CancellationToken cancellationToken)
    {
        var records = await WriteReadAsync(
            "MATCH ()-[r:KG_REL {kgId: $kgId, id: $id}]->() SET r.relationTypeId = $relationTypeId RETURN r.id AS id",
            new { kgId, id = edgeId, relationTypeId },
            cancellationToken);
        return records.Count > 0;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteEdgeAsync(long kgId, string edgeId, CancellationToken cancellationToken)
    {
        var records = await WriteReadAsync(
            "MATCH ()-[r:KG_REL {kgId: $kgId, id: $id}]->() DELETE r RETURN count(*) AS c",
            new { kgId, id = edgeId },
            cancellationToken);
        return records.Count > 0 && records[0]["c"].As<long>() > 0;
    }

    /// <inheritdoc/>
    public async Task<KnowledgeGraphEdgeRecord?> GetEdgeAsync(long kgId, string edgeId, CancellationToken cancellationToken)
    {
        var records = await ReadAsync(
            $"MATCH (s:KgNode {{kgId: $kgId}})-[r:KG_REL {{kgId: $kgId, id: $id}}]->(t:KgNode {{kgId: $kgId}}) RETURN {EdgeReturn}",
            new { kgId, id = edgeId },
            cancellationToken);
        return records.Count == 0 ? null : MapEdge(records[0]);
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<KnowledgeGraphEdgeRecord> Items, long Total)> ListEdgesAsync(long kgId, long? relationTypeId, string? nodeId, int pageNo, int pageSize, CancellationToken cancellationToken)
    {
        const string where = "WHERE ($relationTypeId IS NULL OR r.relationTypeId = $relationTypeId) AND ($nodeId IS NULL OR s.id = $nodeId OR t.id = $nodeId)";
        var parameters = new { kgId, relationTypeId, nodeId = string.IsNullOrWhiteSpace(nodeId) ? null : nodeId, skip = (pageNo - 1) * pageSize, limit = pageSize };

        var countRecords = await ReadAsync($"MATCH (s:KgNode {{kgId: $kgId}})-[r:KG_REL {{kgId: $kgId}}]->(t:KgNode {{kgId: $kgId}}) {where} RETURN count(r) AS c", parameters, cancellationToken);
        var total = countRecords[0]["c"].As<long>();

        var records = await ReadAsync(
            $"MATCH (s:KgNode {{kgId: $kgId}})-[r:KG_REL {{kgId: $kgId}}]->(t:KgNode {{kgId: $kgId}}) {where} RETURN {EdgeReturn} ORDER BY r.id SKIP $skip LIMIT $limit",
            parameters,
            cancellationToken);
        return (records.Select(MapEdge).ToList(), total);
    }

    /// <inheritdoc/>
    public async Task PurgeGraphAsync(long kgId, CancellationToken cancellationToken)
    {
        await WriteAsync("MATCH (n:KgNode {kgId: $kgId}) DETACH DELETE n", new { kgId }, cancellationToken);
    }

    private static KnowledgeGraphNodeRecord MapNode(IRecord record)
        => new(record["id"].As<string>(), record["kgId"].As<long>(), record["entityTypeId"].As<long>(), record["name"].As<string>(), record["description"].As<string>() ?? string.Empty);

    private static KnowledgeGraphEdgeRecord MapEdge(IRecord record)
        => new(record["id"].As<string>(), record["kgId"].As<long>(), record["relationTypeId"].As<long>(), record["sourceNodeId"].As<string>(), record["targetNodeId"].As<string>());

    private async Task<List<IRecord>> ReadAsync(string cypher, object parameters, CancellationToken cancellationToken)
    {
        var driver = await _provider.GetDriverAsync(cancellationToken);
        await using var session = driver.AsyncSession();
        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(cypher, parameters);
            return await cursor.ToListAsync();
        });
    }

    private async Task<List<IRecord>> WriteReadAsync(string cypher, object parameters, CancellationToken cancellationToken)
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

    private async Task WriteAsync(string cypher, object parameters, CancellationToken cancellationToken)
    {
        var driver = await _provider.GetDriverAsync(cancellationToken);
        await using var session = driver.AsyncSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(cypher, parameters);
            await cursor.ConsumeAsync();
        });
    }
}
