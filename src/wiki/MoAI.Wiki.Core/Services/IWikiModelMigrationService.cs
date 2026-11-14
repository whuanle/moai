using System;
using System.Threading;
using System.Threading.Tasks;

namespace MoAI.Wiki.Services;

/// <summary>
/// 知识库向量化模型迁移执行器.
/// </summary>
public interface IWikiModelMigrationService
{
    /// <summary>
    /// 执行迁移任务：原子认领任务 → 分页读旧向量批量重嵌写入临时集合 → 事务换表 → 更新知识库模型配置；
    /// 任一步失败回滚临时集合（原集合原样保留，可重新发起）并置任务失败.
    /// </summary>
    /// <param name="taskId">任务 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task RunAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 将迁移任务置为失败（幂等，仅作用于未完成任务），供消费者重试耗尽/死信兜底.
    /// </summary>
    /// <param name="taskId">任务 id.</param>
    /// <param name="message">失败原因.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task MarkFailedAsync(Guid taskId, string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 校验指定知识库是否存在活跃迁移任务（Wait/Processing），存在则抛 409；
    /// 迁移期间阻断该库的提取/切割/向量化写入，防止切片与向量集合错位.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task EnsureNotMigratingAsync(int wikiId, CancellationToken cancellationToken = default);
}
