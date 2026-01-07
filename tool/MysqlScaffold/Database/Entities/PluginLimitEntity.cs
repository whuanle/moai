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
/// 插件使用量限制.
/// </summary>
[Table("plugin_limit")]
public partial class PluginLimitEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    [Key]
    [Column("id", TypeName = "int(11)")]
    public int Id { get; set; }

    /// <summary>
    /// 插件id.
    /// </summary>
    [Column("plugin_id", TypeName = "int(11)")]
    public int PluginId { get; set; }

    /// <summary>
    /// 用户id.
    /// </summary>
    [Column("user_id", TypeName = "int(11)")]
    public int UserId { get; set; }

    /// <summary>
    /// 限制的规则类型,每天/总额/有效期.
    /// </summary>
    [Column("rule_type", TypeName = "int(11)")]
    public int RuleType { get; set; }

    /// <summary>
    /// 限制值.
    /// </summary>
    [Column("limit_value", TypeName = "int(11)")]
    public int LimitValue { get; set; }

    /// <summary>
    /// 过期时间.
    /// </summary>
    [Column("expiration_time", TypeName = "datetime")]
    public DateTimeOffset ExpirationTime { get; set; }

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
