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
/// 用户.
/// </summary>
[Table("user")]
[Index("Email", Name = "idx_users_email")]
[Index("Phone", Name = "idx_users_phone")]
[Index("UserName", Name = "idx_users_user_name")]
[Index("Email", "IsDeleted", Name = "users_email_is_deleted_uindex", IsUnique = true)]
[Index("Phone", "IsDeleted", Name = "users_phone_is_deleted_uindex", IsUnique = true)]
[Index("UserName", "IsDeleted", Name = "users_user_name_is_deleted_uindex", IsUnique = true)]
public partial class UserEntity : IFullAudited
{
    /// <summary>
    /// 用户ID.
    /// </summary>
    [Key]
    [Column("id", TypeName = "int(11)")]
    public int Id { get; set; }

    /// <summary>
    /// 用户名.
    /// </summary>
    [Column("user_name")]
    [StringLength(50)]
    public string UserName { get; set; } = default!;

    /// <summary>
    /// 邮箱.
    /// </summary>
    [Column("email")]
    public string Email { get; set; } = default!;

    /// <summary>
    /// 密码.
    /// </summary>
    [Column("password")]
    [StringLength(255)]
    public string Password { get; set; } = default!;

    /// <summary>
    /// 昵称.
    /// </summary>
    [Column("nick_name")]
    [StringLength(50)]
    public string NickName { get; set; } = default!;

    /// <summary>
    /// 头像路径.
    /// </summary>
    [Column("avatar_path")]
    [StringLength(255)]
    public string AvatarPath { get; set; } = default!;

    /// <summary>
    /// 手机号.
    /// </summary>
    [Column("phone")]
    [StringLength(20)]
    public string Phone { get; set; } = default!;

    /// <summary>
    /// 禁用.
    /// </summary>
    [Column("is_disable")]
    public bool IsDisable { get; set; }

    /// <summary>
    /// 是否管理员.
    /// </summary>
    [Column("is_admin")]
    public bool IsAdmin { get; set; }

    /// <summary>
    /// 计算密码值的salt.
    /// </summary>
    [Column("password_salt")]
    [StringLength(255)]
    public string PasswordSalt { get; set; } = default!;

    /// <summary>
    /// 软删除.
    /// </summary>
    [Column("is_deleted", TypeName = "bigint(20)")]
    public long IsDeleted { get; set; }

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
}
