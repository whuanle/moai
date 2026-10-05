using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Services;

/// <summary>
/// 知识库检索服务：对给定知识库集合做向量召回，供应用 Agent 的 RAG 上下文与「召回测试」使用.
/// </summary>
public interface IWikiSearchService
{
    /// <summary>
    /// 在指定知识库集合内检索与查询最相似的切片.
    /// </summary>
    /// <param name="wikiIds">知识库 id 集合.</param>
    /// <param name="query">查询文本.</param>
    /// <param name="top">每个知识库返回条数.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>按相似度降序的命中项.</returns>
    Task<IReadOnlyList<WikiSearchHit>> SearchAsync(IReadOnlyCollection<long> wikiIds, string query, int top, CancellationToken cancellationToken = default);

    /// <summary>
    /// 在单个知识库内检索（召回测试）：支持文档范围过滤与相似度阈值，命中项含元数据类型.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="query">查询文本.</param>
    /// <param name="top">返回条数（1-50）.</param>
    /// <param name="minScore">相似度阈值（0-1，含），null 表示不过滤.</param>
    /// <param name="documentIds">文档范围过滤；null 或空表示全部文档.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>按相似度降序的命中项.</returns>
    Task<IReadOnlyList<WikiSearchHit>> SearchInWikiAsync(int wikiId, string query, int top, double? minScore = null, IReadOnlyCollection<long>? documentIds = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按文档 id 与切片序号批量获取原文切片（供 AI 在检索命中片段上下文不足时继续读取同一文档的其它片段）.
    /// </summary>
    /// <param name="wikiIds">允许访问的知识库 id 集合（文档必须属于其中之一）.</param>
    /// <param name="documentId">文档 id.</param>
    /// <param name="chunkIndexes">切片序号集合（从 0 开始，最多 10 个）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回文档切片查询结果.</returns>
    Task<WikiDocumentChunkQueryResult> GetDocumentChunksAsync(IReadOnlyCollection<long> wikiIds, int documentId, IReadOnlyCollection<int> chunkIndexes, CancellationToken cancellationToken = default);
}
