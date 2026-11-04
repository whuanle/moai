namespace MoAI.App.Services;

/// <summary>
/// 应用消息条数计数器.
/// </summary>
public interface IAppMessageUsageCounter
{
    /// <summary>
    /// 按应用 id 累加消息条数增量（可为负：上下文压缩折叠历史时回减）.
    /// </summary>
    /// <param name="appId">应用 id.</param>
    /// <param name="count">增量，正数累加、负数回减.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task IncrementAsync(Guid appId, long count, CancellationToken cancellationToken = default);
}
