using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 流程执行记录.
/// </summary>
public partial class AppWorkflowHistoryEntity : IFullAudited
{
    /// <summary>
    /// varbinary(16).
    /// </summary>
    public Guid Id { get; set; } = default!;

    /// <summary>
    /// 团队id.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 应用id.
    /// </summary>
    public Guid AppId { get; set; } = default!;

    /// <summary>
    /// 流程设计id.
    /// </summary>
    public Guid WorkflowDesignId { get; set; } = default!;

    /// <summary>
    /// 工作状态.
    /// </summary>
    public int State { get; set; }

    /// <summary>
    /// 系统参数.
    /// </summary>
    public string SystemParamters { get; set; } = default!;

    /// <summary>
    /// 运行参数.
    /// </summary>
    public string RunParamters { get; set; } = default!;

    /// <summary>
    /// 数据内容.
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
    /// 更新人.
    /// </summary>
    public int UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
