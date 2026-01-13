namespace MoAI.Hangfire.Services;

/// <summary>
/// 计数器任务激活接口，定时任务会执行这个接口，实现者需要将当前计数刷新到数据库.
/// </summary>
public interface ICounterActivatorJob
{
    /// <summary>
    /// 获取这个计数器的名称.
    /// </summary>
    /// <returns></returns>
    Task<string> GetNameAsync();

    /// <summary>
    /// 刷新计数器，持久化到数据库，要做事务，失败的时候下次可以复用，避免统计错误.
    /// </summary>
    /// <param name="values"></param>
    /// <returns></returns>
    Task ActivateAsync(IReadOnlyDictionary<string, int> values);
}