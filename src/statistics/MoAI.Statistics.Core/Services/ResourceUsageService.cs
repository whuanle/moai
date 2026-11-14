using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MoAI.Statistics.Services;

namespace MoAI.Statistics.Services;

/// <summary>
/// 资源使用统计领域服务默认实现：聚合增量后批量推送 Redis（原子增/减）.
/// </summary>
public class ResourceUsageService : IResourceUsageService
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceUsageService"/> class.
    /// </summary>
    /// <param name="mediator">消息中介器.</param>
    public ResourceUsageService(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task RecordAsync(string name, IReadOnlyDictionary<string, long> counters, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (counters.Count == 0)
        {
            return;
        }

        await _mediator.Send(new IncrementResourceUsageCommand { Name = name, Counters = counters }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task RecordAsync(string name, string resourceId, long count = 1, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
        if (count == 0)
        {
            return Task.CompletedTask;
        }

        return RecordAsync(name, new Dictionary<string, long> { [resourceId] = count }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task RemoveAsync(string name, string resourceId, long count = 1, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return RecordAsync(name, resourceId, -count, cancellationToken);
    }
}
