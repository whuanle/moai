#pragma warning disable CA1031 // 不捕获常规异常类型

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoAI.Statistics.Services;

namespace MoAI.Statistics.Services;

/// <summary>
/// 对话轮次使用量收集器默认实现：内存聚合，作用域结束（Dispose/DisposeAsync）时自动按资源类型批量 flush.
/// 同时实现 IDisposable：宿主以同步方式释放作用域时（如 using var）也能兜底推送.
/// </summary>
public sealed class ResourceUsageCollector : IResourceUsageCollector, IDisposable, IAsyncDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ResourceUsageCollector> _logger;
    private readonly object _lock = new();
    private Dictionary<string, Dictionary<string, long>> _records = new();
    private bool _flushed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceUsageCollector"/> class.
    /// </summary>
    /// <param name="scopeFactory">作用域工厂：flush 时自建独立 scope 推送，避免宿主 scope 释放顺序导致对象已释放.</param>
    /// <param name="logger">日志.</param>
    public ResourceUsageCollector(IServiceScopeFactory scopeFactory, ILogger<ResourceUsageCollector> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool HasRecords
    {
        get
        {
            lock (_lock)
            {
                return _records.Count > 0;
            }
        }
    }

    /// <inheritdoc/>
    public void Record(string name, string resourceId, long count = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
        if (count == 0)
        {
            return;
        }

        lock (_lock)
        {
            if (!_records.TryGetValue(name, out var counters))
            {
                counters = new Dictionary<string, long>();
                _records.Add(name, counters);
            }

            counters[resourceId] = counters.TryGetValue(resourceId, out var current) ? current + count : count;
        }
    }

    /// <inheritdoc/>
    public void RecordBatch(string name, IReadOnlyDictionary<string, long> counters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        foreach (var item in counters)
        {
            if (item.Value != 0)
            {
                Record(name, item.Key, item.Value);
            }
        }
    }

    /// <inheritdoc/>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        Dictionary<string, Dictionary<string, long>> snapshot;
        lock (_lock)
        {
            if (_flushed || _records.Count == 0)
            {
                return;
            }

            _flushed = true;
            snapshot = _records;
            _records = new Dictionary<string, Dictionary<string, long>>();
        }

        // 一轮对话聚合的所有资源，按资源类型各推送一次（批量原子增）；
        // 用独立 scope 解析领域服务：本收集器常在宿主 scope 释放路径上被调用，彼时原 scope 的服务已不可用
        using var scope = _scopeFactory.CreateScope();
        var resourceUsageService = scope.ServiceProvider.GetRequiredService<IResourceUsageService>();
        foreach (var item in snapshot)
        {
            try
            {
                await resourceUsageService.RecordAsync(item.Key, item.Value, cancellationToken);
            }
            catch (Exception ex)
            {
                // 统计推送失败不影响业务流程
                _logger.LogError(ex, "轮次使用量批量推送失败 {Name}", item.Key);
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await FlushAsync().ConfigureAwait(false);
        }
#pragma warning disable CA1031 // 释放路径吞异常
        catch (Exception ex)
        {
            _logger.LogError(ex, "轮次使用量收集器释放时推送失败");
        }
#pragma warning restore CA1031
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_flushed || _records.Count == 0)
            {
                return;
            }
        }

        try
        {
            // 同步释放路径兜底（ASP.NET Core 无同步上下文，GetResult 安全）
            FlushAsync().GetAwaiter().GetResult();
        }
#pragma warning disable CA1031 // 释放路径吞异常
        catch (Exception ex)
        {
            _logger.LogError(ex, "轮次使用量收集器同步释放推送失败");
        }
#pragma warning restore CA1031
    }
}
