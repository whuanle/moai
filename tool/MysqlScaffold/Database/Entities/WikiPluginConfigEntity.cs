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
/// 知识库插件配置.
/// </summary>
[Table("wiki_plugin_config")]
public partial class WikiPluginConfigEntity : IFullAudited
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
    /// 插件标题.
    /// </summary>
    [Column("title")]
    [StringLength(20)]
    public string Title { get; set; } = default!;

    /// <summary>
    /// 配置.
    /// </summary>
    [Column("config", TypeName = "json")]
    public string Config { get; set; } = default!;

    /// <summary>
    /// 插件类型.
    /// </summary>
    [Column("plugin_type")]
    [StringLength(10)]
    public string PluginType { get; set; } = default!;

    /// <summary>
    /// 运行信息.
    /// </summary>
    [Column("work_message")]
    [StringLength(1000)]
    public string WorkMessage { get; set; } = default!;

    /// <summary>
    /// 状态.
    /// </summary>
    [Column("work_state", TypeName = "int(11)")]
    public int WorkState { get; set; }

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
