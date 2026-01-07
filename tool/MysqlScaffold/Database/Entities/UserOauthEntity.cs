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
/// oauth2.0对接.
/// </summary>
[Table("user_oauth")]
[Index("ProviderId", "Sub", "IsDeleted", Name = "user_oauth_provider_id_sub_is_deleted_uindex", IsUnique = true)]
public partial class UserOauthEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    [Key]
    [Column("id", TypeName = "int(11)")]
    public int Id { get; set; }

    /// <summary>
    /// 用户id.
    /// </summary>
    [Column("user_id", TypeName = "int(11)")]
    public int UserId { get; set; }

    /// <summary>
    /// 供应商id,对应oauth_connection表.
    /// </summary>
    [Column("provider_id")]
    public Guid ProviderId { get; set; }

    /// <summary>
    /// 用户oauth对应的唯一id.
    /// </summary>
    [Column("sub")]
    [StringLength(50)]
    public string Sub { get; set; } = default!;

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
