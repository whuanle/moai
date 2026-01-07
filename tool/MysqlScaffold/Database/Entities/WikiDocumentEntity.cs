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
/// 知识库文档.
/// </summary>
[Table("wiki_document")]
public partial class WikiDocumentEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    [Key]
    [Column("id", TypeName = "int(11)")]
    public int Id { get; set; }

    /// <summary>
    /// 知识库id.
    /// </summary>
    [Column("wiki_id", TypeName = "int(11)")]
    public int WikiId { get; set; }

    /// <summary>
    /// 文件id.
    /// </summary>
    [Column("file_id", TypeName = "int(11)")]
    public int FileId { get; set; }

    /// <summary>
    /// 文件路径.
    /// </summary>
    [Column("object_key")]
    [StringLength(100)]
    public string ObjectKey { get; set; } = default!;

    /// <summary>
    /// 文档名称.
    /// </summary>
    [Column("file_name")]
    [StringLength(1024)]
    public string FileName { get; set; } = default!;

    /// <summary>
    /// 文件扩展名称，如.md.
    /// </summary>
    [Column("file_type")]
    [StringLength(10)]
    public string FileType { get; set; } = default!;

    /// <summary>
    /// 是否已经向量化.
    /// </summary>
    [Column("is_embedding")]
    public bool IsEmbedding { get; set; }

    /// <summary>
    /// 版本号，可与向量元数据对比，确认最新文档版本号是否一致.
    /// </summary>
    [Column("version_no", TypeName = "bigint(20)")]
    public long VersionNo { get; set; }

    /// <summary>
    /// 切割配置.
    /// </summary>
    [Column("slice_config", TypeName = "json")]
    public string SliceConfig { get; set; } = default!;

    /// <summary>
    /// 是否有更新，需要重新进行向量化.
    /// </summary>
    [Column("is_update")]
    public bool IsUpdate { get; set; }

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
    /// 最后修改人.
    /// </summary>
    [Column("update_user_id", TypeName = "int(11)")]
    public int UpdateUserId { get; set; }

    /// <summary>
    /// 最后更新时间.
    /// </summary>
    [Column("update_time", TypeName = "datetime")]
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    [Column("is_deleted", TypeName = "bigint(20)")]
    public long IsDeleted { get; set; }
}
