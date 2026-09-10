using System;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 通用任务.
/// </summary>
public partial class WorkerTaskEntity : IFullAudited
{
    /// <summary>
    /// 任务id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 绑定类型.
    /// </summary>
    public string BindType { get; set; } = string.Empty;

    /// <summary>
    /// 绑定id.
    /// </summary>
    public int BindId { get; set; }

    /// <summary>
    /// 状态.
    /// </summary>
    public int State { get; set; }

    /// <summary>
    /// 消息.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 扩展数据（JSON字符串）.
    /// </summary>
    public string Data { get; set; } = "{}";

    /// <summary>
    /// 创建人.
    /// </summary>
    public long CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 最后修改人.
    /// </summary>
    public long UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}