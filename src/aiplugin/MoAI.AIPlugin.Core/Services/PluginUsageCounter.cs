using System;
using System.Threading;
using System.Threading.Tasks;
using MoAI.Statistics.Services;

namespace MoAI.AIPlugin.Services;

/// <summary>
/// 插件使用次数计数器默认实现：委托统计模块领域服务推送 Redis.
/// </summary>
public class PluginUsageCounter : IPluginUsageCounter
{
    private readonly IResourceUsageService _resourceUsageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginUsageCounter"/> class.
    /// </summary>
    /// <param name="resourceUsageService">资源使用统计领域服务.</param>
    public PluginUsageCounter(IResourceUsageService resourceUsageService)
    {
        _resourceUsageService = resourceUsageService;
    }

    /// <inheritdoc/>
    public Task IncrementAsync(Guid pluginId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(pluginId, Guid.Empty);

        return _resourceUsageService.RecordAsync(ResourceUsageNames.Plugin, pluginId.ToString("N"), 1, cancellationToken);
    }
}
