using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

namespace MoAI.Database.Entities;

/// <summary>
/// 切片向量化内容.
/// </summary>
public partial class WikiDocumentChunkEmbeddingEntity : IFullAudited
{
    /// <summary>
    /// 元数据内容唯一ID（derivative_id）.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; set; }

    /// <summary>
    /// 关联文档唯一标识（冗余字段）.
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// 源id，关联自身.
    /// </summary>
    public Guid ChunkId { get; set; }

    /// <summary>
    /// 元数据类型：0=原文，1=大纲，2=问题，3=关键词，4=摘要，5=聚合的段.
    /// </summary>
    public int DerivativeType { get; set; }

    /// <summary>
    /// 提问/提纲/摘要内容.
    /// </summary>
    public string DerivativeContent { get; set; } = default!;

    /// <summary>
    /// 是否被向量化.
    /// </summary>
    public bool IsEmbedding { get; set; }

    /// <summary>
    /// 创建人.
    /// </summary>
    public int CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新人.
    /// </summary>
    public int UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
