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
/// 知识库外部源，飞书文档/爬虫等外部数据入口，持有同步方式与工作流预设.
/// </summary>
public partial class WikiSourceEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; set; }

    /// <summary>
    /// 团队id，冗余便于权限校验与查询.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 外部源类型，见 WikiSourceType（0=飞书文档，1=爬虫）.
    /// </summary>
    public int SourceType { get; set; }

    /// <summary>
    /// 外部源名称，知识库内唯一.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 外部源连接配置 JSON（飞书文档源：飞书应用连接id、节点token、空间id、是否含子文档等）.
    /// </summary>
    public string Config { get; set; } = default!;

    /// <summary>
    /// 外部源工作流配置 JSON（切割/元数据/向量化三步预设），空串表示回退知识库默认工作流.
    /// </summary>
    public string WorkflowConfig { get; set; } = default!;

    /// <summary>
    /// 启用，停用后不再手动/定时/事件同步.
    /// </summary>
    public bool IsEnable { get; set; }

    /// <summary>
    /// 定时同步 cron 表达式（UTC），空串表示未开启定时同步.
    /// </summary>
    public string Cron { get; set; } = default!;

    /// <summary>
    /// 是否开启飞书事件订阅，开启后文档变更事件到达即触发重新拉取.
    /// </summary>
    public bool IsEventSubscription { get; set; }

    /// <summary>
    /// 最近一次同步时间.
    /// </summary>
    public DateTimeOffset? LastSyncTime { get; set; }

    /// <summary>
    /// 最近一次同步状态，见 WikiSourceSyncStatus（0=未同步，1=成功，2=失败）.
    /// </summary>
    public int LastSyncStatus { get; set; }

    /// <summary>
    /// 最近一次同步结果摘要或错误信息.
    /// </summary>
    public string LastSyncMessage { get; set; } = default!;

    /// <summary>
    /// 创建人.
    /// </summary>
    public long CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新人.
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
