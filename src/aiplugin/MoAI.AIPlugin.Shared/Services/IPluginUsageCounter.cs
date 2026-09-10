namespace MoAI.AIPlugin.Services;

/// <summary>
/// 插件使用次数计数器.
/// </summary>
public interface IPluginUsageCounter
{
    /// <summary>
    /// 按插件 id 累加一次使用.
    /// </summary>
    /// <param name="pluginId">插件 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task IncrementAsync(Guid pluginId, CancellationToken cancellationToken = default);
}