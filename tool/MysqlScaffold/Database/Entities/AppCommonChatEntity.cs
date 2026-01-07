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
/// 普通应用对话表.
/// </summary>
[Table("app_common_chat")]
public partial class AppCommonChatEntity : IFullAudited
{
    /// <summary>
    /// id.
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
    /// appid.
    /// </summary>
    [Column("app_id")]
    public Guid AppId { get; set; }

    /// <summary>
    /// 对话标题.
    /// </summary>
    [Column("title")]
    [StringLength(100)]
    public string Title { get; set; } = default!;

    /// <summary>
    /// 输入token统计.
    /// </summary>
    [Column("input_tokens", TypeName = "int(11)")]
    public int InputTokens { get; set; }

    /// <summary>
    /// 输出token统计.
    /// </summary>
    [Column("out_tokens", TypeName = "int(11)")]
    public int OutTokens { get; set; }

    /// <summary>
    /// 使用的 token 总数.
    /// </summary>
    [Column("total_tokens", TypeName = "int(11)")]
    public int TotalTokens { get; set; }

    /// <summary>
    /// 用户类型.
    /// </summary>
    [Column("user_type", TypeName = "int(11)")]
    public int UserType { get; set; }

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
