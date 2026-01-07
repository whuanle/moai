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
/// 文档切片预览.
/// </summary>
[Table("wiki_document_chunk_content_preview")]
[Index("DocumentId", "Id", Name = "idx_doc_slice")]
[Index("WikiId", Name = "idx_wiki_slice")]
public partial class WikiDocumentChunkContentPreviewEntity : IFullAudited
{
    /// <summary>
    /// 切片唯一ID（slice_id）.
    /// </summary>
    [Key]
    [Column("id", TypeName = "bigint(20)")]
    public long Id { get; set; }

    /// <summary>
    /// 关联知识库标识（冗余字段）.
    /// </summary>
    [Column("wiki_id", TypeName = "int(11)")]
    public int WikiId { get; set; }

    /// <summary>
    /// 关联文档唯一标识.
    /// </summary>
    [Column("document_id", TypeName = "int(11)")]
    public int DocumentId { get; set; }

    /// <summary>
    /// 原始切片内容.
    /// </summary>
    [Column("slice_content", TypeName = "text")]
    public string SliceContent { get; set; } = default!;

    /// <summary>
    /// 切片在文档中的顺序.
    /// </summary>
    [Column("slice_order", TypeName = "int(11)")]
    public int SliceOrder { get; set; }

    /// <summary>
    /// 切片字符长度.
    /// </summary>
    [Column("slice_length", TypeName = "int(11)")]
    public int SliceLength { get; set; }

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
