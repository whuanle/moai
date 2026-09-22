using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Maomi.MQ;
using Microsoft.Extensions.Logging;
using MoAI.KnowledgeGraph.Consumers.Events;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 节点向量增量消息发布辅助：发布失败仅记日志，不阻断业务（向量可由后续节点操作或重嵌补齐）.
/// </summary>
public static class KgEmbeddingDeltaPublisher
{
    /// <summary>
    /// 发布单节点 upsert delta.
    /// </summary>
    /// <param name="publisher">消息发布器.</param>
    /// <param name="logger">日志.</param>
    /// <param name="kgId">图谱 id.</param>
    /// <param name="nodeId">节点 id.</param>
    /// <returns>异步任务.</returns>
    public static Task PublishNodeUpsertAsync(IMessagePublisher publisher, ILogger logger, long kgId, string nodeId)
        => PublishAsync(publisher, logger, kgId, [nodeId], null);

    /// <summary>
    /// 发布批量 upsert delta（一条消息带全部节点 id）.
    /// </summary>
    /// <param name="publisher">消息发布器.</param>
    /// <param name="logger">日志.</param>
    /// <param name="kgId">图谱 id.</param>
    /// <param name="nodeIds">节点 id 集.</param>
    /// <returns>异步任务.</returns>
    public static Task PublishNodesUpsertAsync(IMessagePublisher publisher, ILogger logger, long kgId, IReadOnlyList<string> nodeIds)
        => PublishAsync(publisher, logger, kgId, nodeIds, null);

    /// <summary>
    /// 发布节点删除 delta.
    /// </summary>
    /// <param name="publisher">消息发布器.</param>
    /// <param name="logger">日志.</param>
    /// <param name="kgId">图谱 id.</param>
    /// <param name="nodeId">节点 id.</param>
    /// <returns>异步任务.</returns>
    public static Task PublishNodeDeleteAsync(IMessagePublisher publisher, ILogger logger, long kgId, string nodeId)
        => PublishAsync(publisher, logger, kgId, null, [nodeId]);

    private static async Task PublishAsync(IMessagePublisher publisher, ILogger logger, long kgId, IReadOnlyList<string>? upsertNodeIds, IReadOnlyList<string>? deleteNodeIds)
    {
        try
        {
            await publisher.AutoPublishAsync(new KgNodeEmbeddingDeltaMessage
            {
                KgId = kgId,
                UpsertNodeIds = upsertNodeIds?.ToList() ?? [],
                DeleteNodeIds = deleteNodeIds?.ToList() ?? [],
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "发布节点向量增量消息失败（不阻断业务）. KgId={KgId}", kgId);
        }
    }
}
