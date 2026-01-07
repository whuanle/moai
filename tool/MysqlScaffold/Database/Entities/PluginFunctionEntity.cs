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
/// 插件函数.
/// </summary>
[Table("plugin_function")]
public partial class PluginFunctionEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    [Key]
    [Column("id", TypeName = "int(11)")]
    public int Id { get; set; }

    /// <summary>
    /// plugin_custom_id.
    /// </summary>
    [Column("plugin_custom_id", TypeName = "int(11)")]
    public int PluginCustomId { get; set; }

    /// <summary>
    /// 函数名称.
    /// </summary>
    [Column("name")]
    [StringLength(255)]
    public string Name { get; set; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    [Column("summary")]
    [StringLength(1000)]
    public string Summary { get; set; } = default!;

    /// <summary>
    /// api路径.
    /// </summary>
    [Column("path")]
    [StringLength(255)]
    public string Path { get; set; } = default!;

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
