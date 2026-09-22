using System.Collections.Generic;

namespace MoAI.KnowledgeGraph.Models;

/// <summary>
/// 邻居关系摘要.
/// </summary>
/// <param name="RelationName">关系类型名（类型未定义时为 null）.</param>
/// <param name="Direction">out（出边）/ in（入边）.</param>
/// <param name="Name">邻居节点名.</param>
/// <param name="Description">邻居描述.</param>
public sealed record GraphNeighbor(string? RelationName, string Direction, string Name, string Description);

/// <summary>
/// 图检索命中.
/// </summary>
/// <param name="KgId">图谱 id.</param>
/// <param name="NodeId">节点 id.</param>
/// <param name="Name">节点名.</param>
/// <param name="Description">节点描述.</param>
/// <param name="EntityTypeId">实体类型 id.</param>
/// <param name="EntityTypeName">实体类型名.</param>
/// <param name="Score">相似度得分（Cosine Similarity，来自向量库）.</param>
/// <param name="Neighbors">一跳邻居.</param>
public sealed record GraphSearchHit(
    long KgId,
    string NodeId,
    string Name,
    string Description,
    long EntityTypeId,
    string? EntityTypeName,
    double? Score,
    IReadOnlyList<GraphNeighbor> Neighbors);

/// <summary>
/// 图检索结果.
/// </summary>
/// <param name="Hits">命中列表（按得分降序）.</param>
/// <param name="Contents">命中节点文本列表（与 Hits 同序）.</param>
/// <param name="Text">文本化拼接结果（供 LLM 上下文，超长截断）.</param>
/// <param name="SkippedHints">被跳过的图谱及原因（未配置向量化/模型不可用等）.</param>
public sealed record GraphSearchResult(
    IReadOnlyList<GraphSearchHit> Hits,
    IReadOnlyList<string> Contents,
    string Text,
    IReadOnlyList<string> SkippedHints);
