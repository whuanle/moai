namespace MoAI.Wiki.Services;

/// <summary>
/// 知识库使用次数计数器.
/// </summary>
public interface IWikiUsageCounter
{
    /// <summary>
    /// 累加一次知识库成功使用.
    /// </summary>
    /// <param name="wikiId">知识库 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task IncrementAsync(int wikiId, CancellationToken cancellationToken = default);
}