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
/// oauth2.0系统.
/// </summary>
[Table("oauth_connection")]
public partial class OauthConnectionEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    /// <summary>
    /// 认证名称.
    /// </summary>
    [Column("name")]
    [StringLength(50)]
    public string Name { get; set; } = default!;

    /// <summary>
    /// 提供商.
    /// </summary>
    [Column("provider")]
    [StringLength(20)]
    public string Provider { get; set; } = default!;

    /// <summary>
    /// 应用key.
    /// </summary>
    [Column("key")]
    [StringLength(100)]
    public string Key { get; set; } = default!;

    /// <summary>
    /// 密钥.
    /// </summary>
    [Column("secret")]
    [StringLength(100)]
    public string Secret { get; set; } = default!;

    /// <summary>
    /// 图标地址.
    /// </summary>
    [Column("icon_url")]
    [StringLength(1000)]
    public string IconUrl { get; set; } = default!;

    /// <summary>
    /// 登录跳转地址.
    /// </summary>
    [Column("authorize_url")]
    [StringLength(1000)]
    public string AuthorizeUrl { get; set; } = default!;

    /// <summary>
    /// 发现端口.
    /// </summary>
    [Column("well_known")]
    [StringLength(1000)]
    public string WellKnown { get; set; } = default!;

    /// <summary>
    /// 软删除.
    /// </summary>
    [Column("is_deleted", TypeName = "bigint(20)")]
    public long IsDeleted { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    [Column("create_time", TypeName = "datetime")]
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    [Column("update_time", TypeName = "datetime")]
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 创建人.
    /// </summary>
    [Column("create_user_id", TypeName = "int(11)")]
    public int CreateUserId { get; set; }

    /// <summary>
    /// 更新人.
    /// </summary>
    [Column("update_user_id", TypeName = "int(11)")]
    public int UpdateUserId { get; set; }
}
