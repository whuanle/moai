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
/// 插件.
/// </summary>
[Table("plugin")]
[Index("PluginName", Name = "plugin_plugin_name_index")]
[Index("Title", Name = "plugin_title_index")]
public partial class PluginEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    [Key]
    [Column("id", TypeName = "int(11)")]
    public int Id { get; set; }

    /// <summary>
    /// 某个团队创建的自定义插件.
    /// </summary>
    [Column("team_id", TypeName = "int(11)")]
    public int TeamId { get; set; }

    /// <summary>
    /// 对应的实际插件的id，不同类型的插件表不一样.
    /// </summary>
    [Column("plugin_id", TypeName = "int(11)")]
    public int PluginId { get; set; }

    /// <summary>
    /// 插件名称.
    /// </summary>
    [Column("plugin_name")]
    [StringLength(50)]
    public string PluginName { get; set; } = default!;

    /// <summary>
    /// 插件标题.
    /// </summary>
    [Column("title")]
    [StringLength(50)]
    public string Title { get; set; } = default!;

    /// <summary>
    /// 注释.
    /// </summary>
    [Column("description")]
    [StringLength(255)]
    public string Description { get; set; } = default!;

    /// <summary>
    /// mcp|openapi|native|tool.
    /// </summary>
    [Column("type", TypeName = "int(11)")]
    public int Type { get; set; }

    /// <summary>
    /// 分类id.
    /// </summary>
    [Column("classify_id", TypeName = "int(11)")]
    public int ClassifyId { get; set; }

    /// <summary>
    /// 公开访问.
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
