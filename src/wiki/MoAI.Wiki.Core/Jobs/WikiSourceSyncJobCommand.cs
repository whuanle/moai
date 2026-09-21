using System;
using MoAI.Hangfire.Models;

namespace MoAI.Wiki.Jobs;

/// <summary>
/// 外部源定时同步任务的触发参数.
/// </summary>
public class WikiSourceSyncJobParams
{
    /// <summary>
    /// 外部源 id.
    /// </summary>
    public Guid SourceId { get; init; }
}

/// <summary>
/// 外部源定时同步任务：由 Hangfire 按 cron（UTC）触发，调用同步服务增量拉取外部文档.
/// 外部源被删除或停用时不抛异常，而是返回 <see cref="RecuringJobResponse.IsCancel"/> 让调度器移除该任务.
/// </summary>
public class WikiSourceSyncJobCommand : RecuringJobCommand<WikiSourceSyncJobParams>
{
}
