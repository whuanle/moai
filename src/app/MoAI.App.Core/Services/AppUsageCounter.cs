using System;
using System.Threading;
using System.Threading.Tasks;
using MoAI.Statistics.Services;

namespace MoAI.App.Services;

/// <summary>
/// 应用使用次数计数器默认实现：委托统计模块领域服务推送 Redis.
/// </summary>
public class AppUsageCounter : IAppUsageCounter
{
    private readonly IResourceUsageService _resourceUsageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppUsageCounter"/> class.
    /// </summary>
    /// <param name="resourceUsageService">资源使用统计领域服务.</param>
    public AppUsageCounter(IResourceUsageService resourceUsageService)
    {
        _resourceUsageService = resourceUsageService;
    }

    /// <inheritdoc/>
    public Task IncrementAsync(Guid appId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(appId, Guid.Empty);

        return _resourceUsageService.RecordAsync(ResourceUsageNames.App, appId.ToString("N"), 1, cancellationToken);
    }
}
