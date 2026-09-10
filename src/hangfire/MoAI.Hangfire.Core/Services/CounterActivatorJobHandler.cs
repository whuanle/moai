#pragma warning disable CA1031 // 不捕获常规异常类型

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using StackExchange.Redis.Extensions.Core.Abstractions;
using Hangfire;

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
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task InvokeAsync()
    {
        // 获取所有计数器激活器 ICounterActivatorJob
        var activatorJobs = _serviceProvider.GetServices<ICounterActivatorJob>();

        // 并发获取每个激活器
        foreach (var activatorJob in activatorJobs)
        {
            try
            {
                await ActivatorAsync(activatorJob);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Counter activation execution failed {ActivatorJob}", activatorJob.GetType().FullName);
            }
        }
    }

    private async Task ActivatorAsync(ICounterActivatorJob activatorJob)
    {
        var name = await activatorJob.GetNameAsync();
        var values = await _redisDatabase.HashGetAllAsync<long>($"counter:{name}");
        if (values != null && values.Count > 0)
        {
            var positiveValues = values.Where(x => x.Value > 0).ToDictionary().AsReadOnly();
            if (positiveValues.Count == 0)
            {
                return;
            }

            await activatorJob.ActivateAsync(positiveValues);
            await SubtractSnapshotAsync(name, positiveValues);
        }
    }

    private async Task SubtractSnapshotAsync(string name, IReadOnlyDictionary<string, long> values)
    {
        RedisValue[] arguments = new RedisValue[values.Count * 2];
        var index = 0;
        foreach (var item in values)
        {
            arguments[index++] = item.Key;
            arguments[index++] = item.Value;
        }

        const string Script = """
            for i = 1, #ARGV, 2 do
                local remaining = redis.call('HINCRBY', KEYS[1], ARGV[i], '-' .. ARGV[i + 1])
                if remaining <= 0 then
                    redis.call('HDEL', KEYS[1], ARGV[i])
                end
            end
            return 1
            """;
        await _redisDatabase.ScriptEvaluateAsync(
            Script,
            [new RedisKey($"counter:{name}")],
            arguments);
    }
}
