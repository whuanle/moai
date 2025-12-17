using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

namespace MoAI.Database.Entities;

/// <summary>
/// 知识库插件配置.
/// </summary>
public partial class WikiPluginConfigEntity : IFullAudited
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
    /// 插件标题.
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// 配置.
    /// </summary>
    public string Config { get; set; } = default!;

    /// <summary>
    /// 插件类型.
    /// </summary>
    public string PluginType { get; set; } = default!;

    /// <summary>
    /// 插件正在工作.
    /// </summary>
    public bool IsWorking { get; set; }

    /// <summary>
    /// 运行信息.
    /// </summary>
    public string WorkMessage { get; set; } = default!;

    /// <summary>
    /// 状态.
    /// </summary>
    public int WorkState { get; set; }

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
