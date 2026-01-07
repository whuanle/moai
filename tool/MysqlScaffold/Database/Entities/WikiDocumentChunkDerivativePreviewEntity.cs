using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 切片元数据内容表（提问/提纲/摘要）.
/// </summary>
[Table("wiki_document_chunk_derivative_preview")]
[Index("DerivativeType", Name = "idx_deriv_type")]
[Index("DocumentId", "DerivativeType", Name = "idx_doc_deriv")]
[Index("ChunkId", "Id", Name = "idx_slice_deriv")]
public partial class WikiDocumentChunkDerivativePreviewEntity : IFullAudited
{
    /// <summary>
    /// 衍生内容唯一ID（derivative_id）.
    /// </summary>
    [Key]
    [Column("id", TypeName = "bigint(20)")]
    public long Id { get; set; }

    /// <summary>
    /// 知识库id.
    /// </summary>
    [Column("wiki_id", TypeName = "int(11)")]
    public int WikiId { get; set; }

    /// <summary>
    /// 关联文档唯一标识（冗余字段）.
    /// </summary>
    [Column("document_id", TypeName = "int(11)")]
    public int DocumentId { get; set; }

    /// <summary>
    /// 关联切片ID（表A主键）.
    /// </summary>
    [Column("chunk_id", TypeName = "bigint(20)")]
    public long ChunkId { get; set; }

    /// <summary>
    /// 元数据类型：1=大纲，2=问题，3=关键词，4=摘要，5=聚合的段.
    /// </summary>
    [Column("derivative_type", TypeName = "int(11)")]
    public int DerivativeType { get; set; }

    /// <summary>
    /// 提问/提纲/摘要内容.
    /// </summary>
    [Column("derivative_content", TypeName = "text")]
    public string DerivativeContent { get; set; } = default!;

    /// <summary>
    /// 创建人.
    /// </summary>
    [Column("create_user_id", TypeName = "int(11)")]
    public int CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    [Column("create_time", TypeName = "datetime")]
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新人.
    /// </summary>
    [Column("update_user_id", TypeName = "int(11)")]
    public int UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    [Column("update_time", TypeName = "datetime")]
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    [Column("is_deleted", TypeName = "bigint(20)")]
    public long IsDeleted { get; set; }
}
