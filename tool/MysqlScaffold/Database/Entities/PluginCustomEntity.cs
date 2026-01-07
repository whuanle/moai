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
/// 自定义插件.
/// </summary>
[Table("plugin_custom")]
public partial class PluginCustomEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    [Key]
    [Column("id", TypeName = "int(11)")]
    public int Id { get; set; }

    /// <summary>
    /// 服务器地址.
    /// </summary>
    [Column("server")]
    [StringLength(255)]
    public string Server { get; set; } = default!;

    /// <summary>
    /// 头部.
    /// </summary>
    [Column("headers", TypeName = "text")]
    public string Headers { get; set; } = default!;

    /// <summary>
    /// query参数.
    /// </summary>
    [Column("queries", TypeName = "text")]
    public string Queries { get; set; } = default!;

    /// <summary>
    /// mcp|openapi.
    /// </summary>
    [Column("type", TypeName = "int(11)")]
    public int Type { get; set; }

    /// <summary>
    /// 文件id.
    /// </summary>
    [Column("openapi_file_id", TypeName = "int(11)")]
    public int OpenapiFileId { get; set; }

    /// <summary>
    /// 文件名称.
    /// </summary>
    [Column("openapi_file_name")]
    [StringLength(255)]
    public string OpenapiFileName { get; set; } = default!;

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
