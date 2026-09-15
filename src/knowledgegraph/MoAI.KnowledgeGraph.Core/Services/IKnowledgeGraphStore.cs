using MoAI.KnowledgeGraph.Models;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 知识图谱图数据存储（openCypher，方言：memgraph / neo4j）.
/// </summary>
public interface IKnowledgeGraphStore
{
    /// <summary>
    /// 有界子图查询（画布）：按实体类型/关键字取节点子集，边仅返回节点集内部的边.
    /// </summary>
    /// <returns>返回节点、边与是否被截断.</returns>
    Task<(IReadOnlyList<KnowledgeGraphNodeRecord> Nodes, IReadOnlyList<KnowledgeGraphEdgeRecord> Edges, bool Truncated)> QueryCanvasAsync(long KnowledgeGraphId, long? entityTypeId, long? relationTypeId, string? keyword, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// 一跳邻接展开：返回指定节点的邻居节点与相连的边.
    /// </summary>
    /// <returns>返回邻居节点、边与是否被截断.</returns>
    Task<(IReadOnlyList<KnowledgeGraphNodeRecord> Nodes, IReadOnlyList<KnowledgeGraphEdgeRecord> Edges, bool Truncated)> GetNeighborsAsync(long KnowledgeGraphId, string nodeId, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// 统计某实体类型下的节点数.
    /// </summary>
    Task<int> CountNodesByEntityTypeAsync(long KnowledgeGraphId, long entityTypeId, CancellationToken cancellationToken);

    /// <summary>
    /// 统计某关系类型下的边数.
    /// </summary>
    Task<int> CountEdgesByRelationTypeAsync(long KnowledgeGraphId, long relationTypeId, CancellationToken cancellationToken);

    /// <summary>
    /// 创建节点.
    /// </summary>
    Task<KnowledgeGraphNodeRecord> CreateNodeAsync(long KnowledgeGraphId, long entityTypeId, string name, string description, string? propsJson, CancellationToken cancellationToken);

    /// <summary>
    /// 批量创建节点（单语句 UNWIND，单事务），返回记录顺序与输入一致.
    /// </summary>
    Task<IReadOnlyList<KnowledgeGraphNodeRecord>> CreateNodesBatchAsync(long KnowledgeGraphId, IReadOnlyList<KnowledgeGraphNodeInput> nodes, CancellationToken cancellationToken);

    /// <summary>
    /// 批量创建边（单语句 UNWIND，单事务），返回记录顺序与输入一致.
    /// </summary>
    Task<IReadOnlyList<KnowledgeGraphEdgeRecord>> CreateEdgesBatchAsync(long KnowledgeGraphId, IReadOnlyList<KnowledgeGraphEdgeInput> edges, CancellationToken cancellationToken);

    /// <summary>
    /// 更新节点.
    /// </summary>
    Task UpdateNodeAsync(long KnowledgeGraphId, string nodeId, long entityTypeId, string name, string description, string? propsJson, CancellationToken cancellationToken);

    /// <summary>
    /// 删除节点（连带其边），返回是否删除成功.
    /// </summary>
    Task<bool> DeleteNodeAsync(long KnowledgeGraphId, string nodeId, CancellationToken cancellationToken);

    /// <summary>
    /// 获取节点.
    /// </summary>
    Task<KnowledgeGraphNodeRecord?> GetNodeAsync(long KnowledgeGraphId, string nodeId, CancellationToken cancellationToken);

    /// <summary>
    /// 批量获取指定节点的实体类型 id（仅返回存在的节点），用于批量端点预检与关系约束校验.
    /// </summary>
    Task<Dictionary<string, long>> GetNodeTypesByIdsAsync(long KnowledgeGraphId, IReadOnlyList<string> nodeIds, CancellationToken cancellationToken);

    /// <summary>
    /// 分页查询节点.
    /// </summary>
    Task<(IReadOnlyList<KnowledgeGraphNodeRecord> Items, long Total)> ListNodesAsync(long KnowledgeGraphId, long? entityTypeId, string? keyword, int pageNo, int pageSize, CancellationToken cancellationToken);

    /// <summary>
    /// 创建边.
    /// </summary>
    Task<KnowledgeGraphEdgeRecord> CreateEdgeAsync(long KnowledgeGraphId, long relationTypeId, string sourceNodeId, string targetNodeId, CancellationToken cancellationToken);

    /// <summary>
    /// 更新边，返回是否更新成功.
    /// </summary>
    Task<bool> UpdateEdgeAsync(long KnowledgeGraphId, string edgeId, long relationTypeId, CancellationToken cancellationToken);

    /// <summary>
    /// 删除边，返回是否删除成功.
    /// </summary>
    Task<bool> DeleteEdgeAsync(long KnowledgeGraphId, string edgeId, CancellationToken cancellationToken);

    /// <summary>
    /// 获取边.
    /// </summary>
    Task<KnowledgeGraphEdgeRecord?> GetEdgeAsync(long KnowledgeGraphId, string edgeId, CancellationToken cancellationToken);

    /// <summary>
    /// 分页查询边.
    /// </summary>
    Task<(IReadOnlyList<KnowledgeGraphEdgeRecord> Items, long Total)> ListEdgesAsync(long KnowledgeGraphId, long? relationTypeId, string? nodeId, int pageNo, int pageSize, CancellationToken cancellationToken);

    /// <summary>
    /// 清空图谱在图库中的所有节点与边.
    /// </summary>
    Task PurgeGraphAsync(long KnowledgeGraphId, CancellationToken cancellationToken);

    /// <summary>
    /// 探活指定数据库.
    /// </summary>
    Task<bool> ProbeDatabaseAsync(string database, CancellationToken cancellationToken);

    /// <summary>
    /// 内省指定数据库的标签 / 关系类型 / 属性键.
    /// </summary>
    Task<KnowledgeGraphIntrospection> IntrospectAsync(string database, CancellationToken cancellationToken);

    /// <summary>
    /// 接入图有界子图查询（画布）：外部图库全量节点（按标签/关键字过滤），边仅返回节点集内部的边.
    /// </summary>
    /// <returns>返回节点、边与是否被截断.</returns>
    Task<(IReadOnlyList<KnowledgeGraphConnectedNodeRecord> Nodes, IReadOnlyList<KnowledgeGraphConnectedEdgeRecord> Edges, bool Truncated)> QueryConnectedCanvasAsync(string database, string? label, string? keyword, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// 接入图节点一跳邻接展开（按 elementId 定位）.
    /// </summary>
    /// <returns>返回邻居节点、边与是否被截断.</returns>
    Task<(IReadOnlyList<KnowledgeGraphConnectedNodeRecord> Nodes, IReadOnlyList<KnowledgeGraphConnectedEdgeRecord> Edges, bool Truncated)> GetConnectedNeighborsAsync(string database, string nodeId, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// 获取接入图节点（按 elementId 定位），用于邻接查询前的存在性判定（有边无邻居≠不存在）.
    /// </summary>
    Task<KnowledgeGraphConnectedNodeRecord?> GetConnectedNodeAsync(string database, string nodeId, CancellationToken cancellationToken);
}
