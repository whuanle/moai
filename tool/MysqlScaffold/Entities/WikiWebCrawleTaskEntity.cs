using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

namespace MoAI.Database.Entities;

/// <summary>
/// web爬虫状态.
/// </summary>
public partial class WikiWebCrawleTaskEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; set; }

    /// <summary>
    /// web配置id.
    /// </summary>
    public int WikiWebConfigId { get; set; }

    /// <summary>
    /// 任务id，便于追踪.
    /// </summary>
    public Guid TaskTag { get; set; }

    /// <summary>
    /// 爬取状态.
    /// </summary>
    public int CrawleState { get; set; }

    /// <summary>
    /// 任务执行信息.
    /// </summary>
    public string Message { get; set; } = default!;

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
