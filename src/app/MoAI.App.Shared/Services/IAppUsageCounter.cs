namespace MoAI.App.Services;

/// <summary>
/// 应用使用次数计数器.
/// </summary>
public interface IAppUsageCounter
{
    /// <summary>
    /// 按应用 id 累加一次使用.
    /// </summary>
    /// <param name="appId">应用 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task IncrementAsync(Guid appId, CancellationToken cancellationToken = default);
}
