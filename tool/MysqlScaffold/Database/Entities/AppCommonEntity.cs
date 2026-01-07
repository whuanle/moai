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
/// 普通应用.
/// </summary>
[Table("app_common")]
public partial class AppCommonEntity : IFullAudited
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
    /// 所属应用id.
    /// </summary>
    [Column("app_id")]
    public Guid AppId { get; set; }

    /// <summary>
    /// 提示词.
    /// </summary>
    [Column("prompt")]
    [StringLength(4000)]
    public string Prompt { get; set; } = default!;

    /// <summary>
    /// 对话使用的模型 id.
    /// </summary>
    [Column("model_id", TypeName = "int(11)")]
    public int ModelId { get; set; }

    /// <summary>
    /// 要使用的知识库id.
    /// </summary>
    [Column("wiki_ids", TypeName = "json")]
    public string WikiIds { get; set; } = default!;

    /// <summary>
    /// 要使用的插件.
    /// </summary>
    [Column("plugins")]
    [MySqlCollation("utf8mb4_bin")]
    public string Plugins { get; set; } = default!;

    /// <summary>
    /// 对话影响参数.
    /// </summary>
    [Column("execution_settings", TypeName = "json")]
    public string ExecutionSettings { get; set; } = default!;

    /// <summary>
    /// AI头像,存 objectKey.
    /// </summary>
    [Column("avatar")]
    [StringLength(255)]
    public string Avatar { get; set; } = default!;

    /// <summary>
    /// 是否开启授权验证.
    /// </summary>
    [Column("is_auth")]
    public bool IsAuth { get; set; }

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
