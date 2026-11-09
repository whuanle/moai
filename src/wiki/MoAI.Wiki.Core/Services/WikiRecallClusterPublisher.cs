using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Maomi.MQ;
using Microsoft.Extensions.Logging;
using MoAI.Wiki.Consumers.Events;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Services;

/// <summary>
/// 知识库检索经验聚类消息发布辅助：发布失败仅记日志，不阻断检索主流程（漏记一次沉淀仅损失一次复用机会）.
/// </summary>
public static class WikiRecallClusterPublisher
{
    /// <summary>
    /// 发布一次完整检索的沉淀消息.
    /// </summary>
    /// <param name="publisher">消息发布器.</param>
    /// <param name="logger">日志.</param>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="query">查询原文（发布前归一化）.</param>
    /// <param name="hits">最终命中列表（按最终排序，超限截断）.</param>
    /// <returns>异步任务.</returns>
    public static async Task PublishAsync(IMessagePublisher publisher, ILogger logger, int wikiId, string query, IReadOnlyList<WikiSearchHit> hits)
    {
        if (hits.Count == 0)
        {
            return;
        }

        var normalized = WikiRecallClusterService.NormalizeQuery(query);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        try
        {
            await publisher.AutoPublishAsync(new WikiRecallClusterMessage
            {
                WikiId = wikiId,
                Query = normalized,
                Hits = hits.Take(WikiRecallClusterDefaults.MaxHits).Select(x => new WikiRecallClusterHit
                {
                    DocumentId = x.DocumentId,
                    ChunkId = x.ChunkId,
                    Score = x.Score,
                    RerankScore = x.RerankScore,
                    MetadataType = x.MetadataType,
                }).ToList(),
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "发布检索经验聚类消息失败（不阻断检索）. WikiId={WikiId}", wikiId);
        }
    }
}
