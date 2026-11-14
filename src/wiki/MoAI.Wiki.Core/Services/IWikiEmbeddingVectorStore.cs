using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MoAI.Wiki.Services;

/// <summary>
/// 知识库向量存储抽象：每个知识库一个集合（表），由 pgvector 提供者自动建表.
/// </summary>
public interface IWikiEmbeddingVectorStore
{
    /// <summary>
    /// 确保知识库向量集合存在（不存在则按维度创建表与 hnsw 索引）.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="dimensions">向量维度.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task EnsureCollectionAsync(int wikiId, int dimensions, CancellationToken cancellationToken = default);

    /// <summary>
    /// 用新记录整体替换某文档的向量（先删后写）.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="documentId">文档 id.</param>
    /// <param name="records">向量记录.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task ReplaceDocumentVectorsAsync(int wikiId, int documentId, IReadOnlyList<WikiEmbeddingVectorRecord> records, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除某文档的全部向量.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="documentId">文档 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task DeleteDocumentVectorsAsync(int wikiId, int documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除知识库整个向量集合（DROP 表，用于删除知识库时清理，不存在的集合静默跳过）.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task DeleteWikiVectorsAsync(int wikiId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 统计某文档的向量数量.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="documentId">文档 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>向量数量.</returns>
    Task<int> CountDocumentVectorsAsync(int wikiId, int documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 统计知识库整个集合的向量数量（表不存在返回 0）.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>向量数量.</returns>
    Task<int> CountCollectionAsync(int wikiId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按 Key 升序分页遍历集合全部记录（模型迁移期间原集合只读，skip 分页不重不漏）.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="skip">跳过条数.</param>
    /// <param name="max">本页最大条数.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>记录列表.</returns>
    Task<IReadOnlyList<WikiEmbeddingVectorRecord>> ListAllPageAsync(int wikiId, int skip, int max, CancellationToken cancellationToken = default);

    /// <summary>
    /// 开始迁移：兜底删除残留临时集合（__wiki_mig_{id}）并按目标维度重建（embedding 模型迁移用）.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="dimensions">目标向量维度.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task BeginMigrationCollectionAsync(int wikiId, int dimensions, CancellationToken cancellationToken = default);

    /// <summary>
    /// 将迁移批次写入临时集合（embedding 模型迁移用）.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="records">向量记录.</param>
    /// <param name="dimensions">目标向量维度.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task UpsertMigrationCollectionAsync(int wikiId, IReadOnlyList<WikiEmbeddingVectorRecord> records, int dimensions, CancellationToken cancellationToken = default);

    /// <summary>
    /// 完成迁移：单事务 DROP 旧集合并将临时集合换名顶替（embedding 模型迁移用）.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task CompleteMigrationCollectionAsync(int wikiId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 放弃迁移：仅删除临时集合，原集合不受影响（embedding 模型迁移用）.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task AbortMigrationCollectionAsync(int wikiId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按查询向量召回最相似的记录.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="queryVector">查询向量.</param>
    /// <param name="top">返回条数.</param>
    /// <param name="documentIds">文档范围过滤；null 或空表示不过滤.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>召回结果.</returns>
    Task<IReadOnlyList<WikiEmbeddingSearchResult>> SearchAsync(int wikiId, ReadOnlyMemory<float> queryVector, int top, IReadOnlyCollection<int>? documentIds = null, CancellationToken cancellationToken = default);
}
