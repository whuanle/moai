using System;
using System.Threading;
using System.Threading.Tasks;

namespace MoAI.Memory.Services;

/// <summary>
/// 长期记忆向量化模型迁移执行器.
/// </summary>
public interface IMemoryModelMigrationService
{
    /// <summary>
    /// 执行迁移任务：原子认领任务 → 逐集合用新模型重嵌写入临时表 → 事务换表 → 切换平台设置；
    /// 任一步失败回滚全部临时表（原集合原样保留，可重新发起）并置任务失败.
    /// </summary>
    Task RunAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 将迁移任务置为失败（幂等，仅作用于未完成任务），供消费者重试耗尽/死信兜底.
    /// </summary>
    Task MarkFailedAsync(Guid taskId, string message, CancellationToken cancellationToken = default);
}
