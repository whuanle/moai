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
}
