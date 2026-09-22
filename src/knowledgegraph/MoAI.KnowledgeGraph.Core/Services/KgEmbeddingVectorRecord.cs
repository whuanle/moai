using System;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 知识图谱向量记录（每个图谱一个 pgvector 集合 __kg_{id} 中的一行）.
/// </summary>
public class KgEmbeddingVectorRecord
{
    /// <summary>
    /// 主键.
    /// </summary>
    public Guid Key { get; set; }

    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; set; }

    /// <summary>
    /// 节点 id（KgNode.id，召回溯源的真源）.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// 实体类型 id.
    /// </summary>
    public long EntityTypeId { get; set; }

    /// <summary>
    /// 节点名.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 被向量化的文本（名称 + 描述）.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 向量.
    /// </summary>
    public ReadOnlyMemory<float> Embedding { get; set; }
}
