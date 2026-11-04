using System;
using System.Threading;
using System.Threading.Tasks;
using MoAI.Statistics.Services;

namespace MoAI.App.Services;

/// <summary>
/// 应用消息条数计数器默认实现：委托统计模块领域服务推送 Redis.
/// </summary>
public class AppMessageUsageCounter : IAppMessageUsageCounter
{
    private readonly IResourceUsageService _resourceUsageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppMessageUsageCounter"/> class.
    /// </summary>
    /// <param name="resourceUsageService">资源使用统计领域服务.</param>
    public AppMessageUsageCounter(IResourceUsageService resourceUsageService)
    {
        _resourceUsageService = resourceUsageService;
    }

    /// <inheritdoc/>
    public Task IncrementAsync(Guid appId, long count, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(appId, Guid.Empty);

        if (count == 0)
        {
            return Task.CompletedTask;
        }

        return _resourceUsageService.RecordAsync(ResourceUsageNames.AppMessage, appId.ToString("N"), count, cancellationToken);
    }
}
