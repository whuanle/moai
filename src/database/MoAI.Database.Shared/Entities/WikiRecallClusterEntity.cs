using System;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 知识库检索经验聚类：验证过的查询→命中沉淀为可复用知识单元（query 归一化哈希唯一），
/// 相似查询免 embedding 直接复用；复用时回源校验切片存活，来源变化即失效自愈.
/// </summary>
public partial class WikiRecallClusterEntity : IFullAudited
{
    /// <summary>
    /// 雪花主键.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; set; }

    /// <summary>
    /// 归一化查询的 SHA-256（与 wiki_id 组成唯一键）.
    /// </summary>
    public string QueryHash { get; set; } = default!;

    /// <summary>
    /// 归一化后的查询原文（展示用）.
    /// </summary>
    public string Query { get; set; } = default!;

    /// <summary>
    /// 沉淀时的知识库索引纪元（wiki.index_epoch）；复用时与当前纪元比对，不匹配即失效自愈（保证不吞新增内容）.
    /// </summary>
    public long IndexEpoch { get; set; }

    /// <summary>
    /// 沉淀的命中列表 JSON：[{documentId, chunkId, score, rerankScore, metadataType}]，最多 20 条.
    /// </summary>
    public string HitsJson { get; set; } = default!;

    /// <summary>
    /// 沉淀的命中条数.
    /// </summary>
    public int HitCount { get; set; }

    /// <summary>
    /// 累计服务次数（含首次沉淀；复用命中时递增）.
    /// </summary>
    public int RecallCount { get; set; }

    /// <summary>
    /// 最近一次服务（沉淀或复用）时间.
    /// </summary>
    public DateTimeOffset LastRecallTime { get; set; }

    /// <summary>
    /// 创建人.
    /// </summary>
    public long CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新人.
    /// </summary>
    public long UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
