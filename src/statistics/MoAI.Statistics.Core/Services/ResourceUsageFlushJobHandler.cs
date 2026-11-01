#pragma warning disable CA1031 // 不捕获常规异常类型

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Hangfire;
using MediatR;
using MoAI.Settings.Models;
using MoAI.Settings.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoAI.Statistics.Services;
using StackExchange.Redis;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Statistics.Services;

/// <summary>
/// 资源使用统计批量落库任务：定期从 Redis 原子取出各资源类型计数（counter:{name}），
/// 交给对应 <see cref="IResourceUsageWriter"/> 写入资源自身表，并同步累加统一统计表.
/// </summary>
public class ResourceUsageFlushJobHandler
{
    private const string KeyPrefix = "counter:";

    private readonly IRedisDatabase _redisDatabase;
    private readonly ResourceUsageStore _store;
    private readonly IStatisticsSettingsService _statisticsSettingsService;
    private readonly IMediator _mediator;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ResourceUsageFlushJobHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceUsageFlushJobHandler"/> class.
    /// </summary>
    /// <param name="redisDatabase">Redis 数据库.</param>
    /// <param name="store">统一统计表落库服务.</param>
    /// <param name="statisticsSettingsService">统计设置读取服务（落库粒度）.</param>
    /// <param name="mediator">消息中介器，用于落库失败时回补计数.</param>
    /// <param name="serviceProvider">服务提供器，用于发现全部写入器.</param>
    /// <param name="logger">日志.</param>
    public ResourceUsageFlushJobHandler(
        IRedisDatabase redisDatabase,
        ResourceUsageStore store,
        IStatisticsSettingsService statisticsSettingsService,
        IMediator mediator,
        IServiceProvider serviceProvider,
        ILogger<ResourceUsageFlushJobHandler> logger)
    {
        _redisDatabase = redisDatabase;
        _store = store;
        _statisticsSettingsService = statisticsSettingsService;
        _mediator = mediator;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// 执行器.
    /// </summary>
    /// <returns>异步任务.</returns>
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task InvokeAsync()
    {
        foreach (var writer in _serviceProvider.GetServices<IResourceUsageWriter>())
        {
            try
            {
                await FlushAsync(writer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "资源使用统计落库失败 {Writer}", writer.GetType().FullName);
            }
        }
    }

    private async Task FlushAsync(IResourceUsageWriter writer)
    {
        var name = await writer.GetNameAsync();
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        var values = await TakeSnapshotAsync(name);
        if (values.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var granularity = await _statisticsSettingsService.GetGranularityAsync();
        var usageHour = granularity == ResourceUsageGranularity.Hour ? now.UtcDateTime.Hour : ResourceUsageStore.WholeDayHour;

        try
        {
            await writer.WriteAsync(values);
            await _store.RecordAsync(DateOnly.FromDateTime(now.UtcDateTime), usageHour, name, values);
        }
        catch
        {
            // 落库失败把增量原样回补 Redis，下个周期重新取出，保证计数不丢
            await RestoreAsync(name, values);
            throw;
        }
    }

    /// <summary>
    /// 原子取出：HGETALL 后立刻 DEL（取出即删），正常路径不会重复累加；非数字脏数据顺带清除，正负增量都保留.
    /// </summary>
    /// <param name="name">资源类型名.</param>
    /// <returns>key 是资源 id 文本，value 是增量（正增负减）.</returns>
    private async Task<IReadOnlyDictionary<string, long>> TakeSnapshotAsync(string name)
    {
        const string Script = """
            local values = redis.call('HGETALL', KEYS[1])
            if next(values) then
                redis.call('DEL', KEYS[1])
            end
            return values
            """;
        var result = await _redisDatabase.ScriptEvaluateAsync(
            Script,
            [new RedisKey(KeyPrefix + name)],
            Array.Empty<RedisValue>());

        var raw = (RedisValue[])result;
        Dictionary<string, long> values = new();
        for (var i = 0; i + 1 < raw.Length; i += 2)
        {
            if (!long.TryParse(raw[i + 1].ToString(), out var count) || count == 0)
            {
                continue;
            }

            values[raw[i].ToString()] = count;
        }

        return values;
    }

    private Task RestoreAsync(string name, IReadOnlyDictionary<string, long> values)
    {
        return _mediator.Send(new IncrementResourceUsageCommand { Name = name, Counters = values });
    }
}
