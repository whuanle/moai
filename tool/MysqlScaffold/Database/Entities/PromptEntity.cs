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
/// 提示词.
/// </summary>
[Table("prompt")]
public partial class PromptEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    [Key]
    [Column("id", TypeName = "int(11)")]
    public int Id { get; set; }

    /// <summary>
    /// 名称.
    /// </summary>
    [Column("name")]
    [StringLength(20)]
    public string Name { get; set; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    [Column("description")]
    [StringLength(255)]
    public string Description { get; set; } = default!;

    /// <summary>
    /// 提示词内容.
    /// </summary>
    [Column("content", TypeName = "text")]
    public string Content { get; set; } = default!;

    /// <summary>
    /// 分类id.
    /// </summary>
    [Column("prompt_class_id", TypeName = "int(11)")]
    public int PromptClassId { get; set; }

    /// <summary>
    /// 是否公开.
    /// </summary>
    [Column("is_public")]
    public bool IsPublic { get; set; }

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
