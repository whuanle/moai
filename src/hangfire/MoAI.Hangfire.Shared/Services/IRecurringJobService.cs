using MoAI.Hangfire.Models;

namespace MoAI.Hangfire.Services;

/// <summary>
/// 定时任务服务接口.
/// </summary>
public interface IRecurringJobService
{
    /// <summary>
    /// 创建一个新的定时任务.
    /// </summary>
    /// <typeparam name="TCommand">定时任务要触发的命令</typeparam>
    /// <typeparam name="TParams">触发参数类型</typeparam>
    /// <param name="key">任务key.</param>
    /// <param name="cron">cron表达式</param>
    /// <param name="params">触发参数.</param>
    /// <returns></returns>
    Task AddOrUpdateRecurringJobAsync<TCommand, TParams>(string key, string cron, TParams @params)
        where TCommand : RecuringJobCommand<TParams>, new();

    /// <summary>
    /// 创建一个新的定时任务，指定开始时间，只支持精细到分钟.
    /// </summary>
    /// <typeparam name="TCommand">定时任务要触发的命令</typeparam>
    /// <typeparam name="TParams">触发参数类型</typeparam>
    /// <param name="key">任务key.</param>
    /// <param name="startTime">第一次执行的开始时间.</param>
    /// <param name="cron">cron表达式</param>
    /// <param name="params">触发参数.</param>
    /// <returns></returns>
    Task AddOrUpdateRecurringJobAsync<TCommand, TParams>(string key, DateTimeOffset startTime, string cron, TParams @params)
        where TCommand : RecuringJobCommand<TParams>, new();

    /// <summary>
    /// 创建一个在某个时间只执行一次的任务，只支持精细到分钟.
    /// </summary>
    /// <typeparam name="TCommand">定时任务要触发的命令</typeparam>
    /// <typeparam name="TParams">触发参数类型</typeparam>
    /// <param name="key">任务key.</param>
    /// <param name="startTime">第一次执行的开始时间.</param>
    /// <param name="params">触发参数.</param>
    /// <returns></returns>
    Task AddOrUpdateRecurringJobAsync<TCommand, TParams>(string key, DateTimeOffset startTime, TParams @params)
        where TCommand : RecuringJobCommand<TParams>, new();

    /// <summary>
    /// 根据 key 取消任务.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    Task RemoveRecurringJobAsync(string key);
}