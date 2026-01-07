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
/// 工作任务.
/// </summary>
[Table("worker_task")]
public partial class WorkerTaskEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    /// <summary>
    /// 关联类型.
    /// </summary>
    [Column("bind_type")]
    [StringLength(20)]
    public string BindType { get; set; } = default!;

    /// <summary>
    /// 关联对象id.
    /// </summary>
    [Column("bind_id", TypeName = "int(11)")]
    public int BindId { get; set; }

    /// <summary>
    /// 任务状态，不同的任务类型状态值规则不一样.
    /// </summary>
    [Column("state", TypeName = "int(11)")]
    public int State { get; set; }

    /// <summary>
    /// 消息、错误信息.
    /// </summary>
    [Column("message", TypeName = "text")]
    public string Message { get; set; } = default!;

    /// <summary>
    /// 自定义数据,json格式.
    /// </summary>
    [Column("data", TypeName = "json")]
    public string Data { get; set; } = default!;

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
