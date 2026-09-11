using MoAI.KnowledgeGraph.Models;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 知识图谱图数据存储（Neo4j）.
/// </summary>
public interface IKnowledgeGraphStore
{
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
    Task<KnowledgeGraphNodeRecord> CreateNodeAsync(long KnowledgeGraphId, long entityTypeId, string name, string description, CancellationToken cancellationToken);

    /// <summary>
    /// 更新节点.
    /// </summary>
    Task UpdateNodeAsync(long KnowledgeGraphId, string nodeId, long entityTypeId, string name, string description, CancellationToken cancellationToken);

    /// <summary>
    /// 删除节点（连带其边），返回是否删除成功.
    /// </summary>
    Task<bool> DeleteNodeAsync(long KnowledgeGraphId, string nodeId, CancellationToken cancellationToken);

    /// <summary>
    /// 获取节点.
    /// </summary>
    Task<KnowledgeGraphNodeRecord?> GetNodeAsync(long KnowledgeGraphId, string nodeId, CancellationToken cancellationToken);

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
}
