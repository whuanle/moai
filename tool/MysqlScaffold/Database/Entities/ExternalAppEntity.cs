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
/// 系统接入.
/// </summary>
[Table("external_app")]
public partial class ExternalAppEntity : IFullAudited
{
    /// <summary>
    /// app_id.
    /// </summary>
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    /// <summary>
    /// 团队id.
    /// </summary>
    [Column("team_id", TypeName = "int(11)")]
    public int TeamId { get; set; }

    /// <summary>
    /// 应用名称.
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
    /// 头像objectKey.
    /// </summary>
    [Column("avatar")]
    [StringLength(255)]
    public string Avatar { get; set; } = default!;

    /// <summary>
    /// 应用密钥.
    /// </summary>
    [Column("key")]
    [StringLength(255)]
    public string Key { get; set; } = default!;

    /// <summary>
    /// 禁用.
    /// </summary>
    [Column("is_dsiable")]
    public bool IsDsiable { get; set; }

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
