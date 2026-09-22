using System;
using System.Collections.Generic;
using Maomi.MQ;

namespace MoAI.KnowledgeGraph.Consumers.Events;

/// <summary>
/// 知识图谱节点向量增量消息：upsert 读图库最新状态幂等处理，多次 delta 自然合并.
/// </summary>
[RouterKey("kg.node.embedding")]
public class KgNodeEmbeddingDeltaMessage
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }

    /// <summary>
    /// 需要重建向量的节点 id.
    /// </summary>
    public List<string> UpsertNodeIds { get; init; } = [];

    /// <summary>
    /// 需要删除向量的节点 id.
    /// </summary>
    public List<string> DeleteNodeIds { get; init; } = [];
}
