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
/// 文件列表.
/// </summary>
[Table("file")]
public partial class FileEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    [Key]
    [Column("id", TypeName = "int(11)")]
    public int Id { get; set; }

    /// <summary>
    /// 文件路径.
    /// </summary>
    [Column("object_key")]
    [StringLength(1024)]
    public string ObjectKey { get; set; } = default!;

    /// <summary>
    /// 文件扩展名.
    /// </summary>
    [Column("file_extension")]
    [StringLength(10)]
    public string FileExtension { get; set; } = default!;

    /// <summary>
    /// md5.
    /// </summary>
    [Column("file_md5")]
    [StringLength(50)]
    public string FileMd5 { get; set; } = default!;

    /// <summary>
    /// 文件大小.
    /// </summary>
    [Column("file_size", TypeName = "int(11)")]
    public int FileSize { get; set; }

    /// <summary>
    /// 文件类型.
    /// </summary>
    [Column("content_type")]
    [StringLength(50)]
    public string ContentType { get; set; } = default!;

    /// <summary>
    /// 是否已经上传完毕.
    /// </summary>
    [Column("is_uploaded")]
    public bool IsUploaded { get; set; }

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
