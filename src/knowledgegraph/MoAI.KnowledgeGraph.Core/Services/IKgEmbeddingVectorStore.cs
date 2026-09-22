using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 知识图谱向量存储抽象：每个图谱一个集合（表），由 pgvector 提供者自动建表.
/// </summary>
public interface IKgEmbeddingVectorStore
{
    /// <summary>
    /// 确保图谱向量集合存在（不存在则按维度创建表与 hnsw 索引）.
    /// </summary>
    /// <param name="kgId">图谱 id.</param>
    /// <param name="dimensions">向量维度.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task EnsureCollectionAsync(int kgId, int dimensions, CancellationToken cancellationToken = default);

    /// <summary>
    /// 用新记录整体替换某节点的向量（先删后写）.
    /// </summary>
    /// <param name="kgId">图谱 id.</param>
    /// <param name="nodeId">节点 id.</param>
    /// <param name="records">向量记录.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task ReplaceNodeVectorsAsync(int kgId, string nodeId, IReadOnlyList<KgEmbeddingVectorRecord> records, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除某节点的全部向量.
    /// </summary>
    /// <param name="kgId">图谱 id.</param>
    /// <param name="nodeId">节点 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task DeleteNodeVectorsAsync(int kgId, string nodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除图谱的整个向量集合（删图时调用）.
    /// </summary>
    /// <param name="kgId">图谱 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task DeleteGraphVectorsAsync(int kgId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按查询向量召回最相似的记录.
    /// </summary>
    /// <param name="kgId">图谱 id.</param>
    /// <param name="queryVector">查询向量.</param>
    /// <param name="top">返回条数.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>召回结果.</returns>
    Task<IReadOnlyList<KgEmbeddingSearchResult>> SearchAsync(int kgId, ReadOnlyMemory<float> queryVector, int top, CancellationToken cancellationToken = default);
}
