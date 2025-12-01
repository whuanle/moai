using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

namespace MoAI.Database.Entities;

/// <summary>
/// 工作任务.
/// </summary>
public partial class WorkerTaskEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 关联类型.
    /// </summary>
    public string BindType { get; set; } = default!;

    /// <summary>
    /// 关联对象id.
    /// </summary>
    public int BindId { get; set; }

    /// <summary>
    /// 任务状态，不同的任务类型状态值规则不一样.
    /// </summary>
    public int State { get; set; }

    /// <summary>
    /// 消息、错误信息.
    /// </summary>
    public string Message { get; set; } = default!;

    /// <summary>
    /// 自定义数据,json格式.
    /// </summary>
    public string Data { get; set; } = default!;

    /// <summary>
    /// 创建人.
    /// </summary>
    public int CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 最后修改人.
    /// </summary>
    public int UpdateUserId { get; set; }

    /// <summary>
    /// 最后更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
