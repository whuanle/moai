#pragma warning disable CA1031 // 不捕获常规异常类型

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Hangfire.Services;

/// <summary>
/// 计数器处理任务.
/// </summary>
public class CounterActivatorJobHandler
{
    private readonly IRedisDatabase _redisDatabase;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CounterActivatorJobHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CounterActivatorJobHandler"/> class.
    /// </summary>
    /// <param name="redisDatabase"></param>
    /// <param name="serviceProvider"></param>
    /// <param name="logger"></param>
    public CounterActivatorJobHandler(IRedisDatabase redisDatabase, IServiceProvider serviceProvider, ILogger<CounterActivatorJobHandler> logger)
    {
        _redisDatabase = redisDatabase;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// 执行器.
    /// </summary>
    /// <returns></returns>
    public async Task InvokeAsync()
    {
        // 获取所有计数器激活器 ICounterActivatorJob
        CancellationToken cancellationToken = CancellationToken.None;
        var activatorJobs = _serviceProvider.GetServices<ICounterActivatorJob>();

        // 并发获取每个激活器
        List<Task> tasks = new();
        foreach (var activatorJob in activatorJobs)
        {
            var task = Task.Factory.StartNew(
                async () =>
                {
                    try
                    {
                        await ActivatorAsync(activatorJob);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Counter activation execution failed {ActivatorJob}", activatorJob.GetType().FullName);
                    }
                },
                cancellationToken,
                TaskCreationOptions.None,
                TaskScheduler.Default).Unwrap();

            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
    }

    private async Task ActivatorAsync(ICounterActivatorJob activatorJob)
    {
        var name = await activatorJob.GetNameAsync();
        var values = await _redisDatabase.HashGetAllAsync<int>($"counter:{name}");
        if (values != null && values.Count > 0)
        {
            await activatorJob.ActivateAsync(values.AsReadOnly());

            // 获取服务器最新统计值
            var lastValues = await _redisDatabase.HashGetAllAsync<int>($"counter:{name}");

            // 将 lastValues 中的值从 values 中减去
            foreach (var oldCounter in values)
            {
                if (lastValues.TryGetValue(oldCounter.Key, out var newCounter))
                {
                    var newValue = newCounter - oldCounter.Value;
                    if (newValue <= 0)
                    {
                        lastValues[oldCounter.Key] = 0;
                    }
                    else
                    {
                        lastValues[oldCounter.Key] = newValue;
                    }
                }
            }

            // 批量设置某个 name 的值
            await _redisDatabase.HashSetAsync($"counter:{name}", lastValues);
        }
    }
}
