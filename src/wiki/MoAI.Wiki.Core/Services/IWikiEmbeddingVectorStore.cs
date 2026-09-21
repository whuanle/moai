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
    /// 统计某文档的向量数量.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="documentId">文档 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>向量数量.</returns>
    Task<int> CountDocumentVectorsAsync(int wikiId, int documentId, CancellationToken cancellationToken = default);

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
