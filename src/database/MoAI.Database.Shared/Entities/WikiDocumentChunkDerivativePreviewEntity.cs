using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

namespace MoAI.Database.Entities;

/// <summary>
/// 切片衍生内容表（提问/提纲/摘要）.
/// </summary>
public partial class WikiDocumentChunkDerivativePreviewEntity : IFullAudited
{
    /// <summary>
    /// 衍生内容唯一ID（derivative_id）.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; set; }

    /// <summary>
    /// 关联文档唯一标识（冗余字段）.
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// 关联切片ID（表A主键）.
    /// </summary>
    public long ChunkId { get; set; }

    /// <summary>
    /// 衍生类型：1=大纲，2=问题，3=关键词，4=摘要，5=聚合的段.
    /// </summary>
    public int DerivativeType { get; set; }

    /// <summary>
    /// 提问/提纲/摘要内容.
    /// </summary>
    public string DerivativeContent { get; set; } = default!;

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
