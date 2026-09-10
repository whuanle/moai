namespace MoAI.AIChannel.Services;

/// <summary>
/// AI 模型使用计数器.
/// </summary>
public interface IAiModelUsageCounter
{
    /// <summary>
    /// 累加一次模型使用及其 token 数.
    /// </summary>
    /// <param name="modelId">模型 id.</param>
    /// <param name="teamId">团队 id.</param>
    /// <param name="userId">用户 id.</param>
    /// <param name="useType">使用来源类型.</param>
    /// <param name="useResourceId">使用来源资源 id.</param>
    /// <param name="promptTokens">输入 token 数.</param>
    /// <param name="completionTokens">输出 token 数.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task IncrementAsync(
        Guid modelId,
        int teamId,
        long userId,
        int useType,
        Guid useResourceId,
        int promptTokens,
        int completionTokens,
        CancellationToken cancellationToken = default);
}