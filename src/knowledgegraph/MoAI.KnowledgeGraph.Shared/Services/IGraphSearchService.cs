using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoAI.KnowledgeGraph.Models;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 知识图谱向量检索服务：查询向量 → top 实体 → 一跳关系扩展 → 子图文本化（GraphRAG local search 轻量版）.
/// 团队归属由调用方保证（应用绑定保存时校验 / 工作流 Client 守卫 / 检索 API Authorizer）.
/// </summary>
public interface IGraphSearchService
{
    /// <summary>
    /// 在多张图谱中做语义检索.
    /// </summary>
    /// <param name="graphIds">图谱 id 集（去重去非正）.</param>
    /// <param name="query">查询文本.</param>
    /// <param name="topPerGraph">每图召回条数.</param>
    /// <param name="minScore">相似度阈值（null 不过滤）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>检索结果（含跳过提示）.</returns>
    Task<GraphSearchResult> SearchAsync(IReadOnlyCollection<long> graphIds, string query, int topPerGraph = 5, double? minScore = null, CancellationToken cancellationToken = default);
}
