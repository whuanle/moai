using MediatR;
using MoAI.Hangfire.Services;

namespace MoAI.App.Services;

/// <summary>
/// 应用使用次数计数器默认实现.
/// </summary>
public class AppUsageCounter : IAppUsageCounter
{
    private const string CounterName = "app-usage";
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppUsageCounter"/> class.
    /// </summary>
    /// <param name="mediator">消息中介器.</param>
    public AppUsageCounter(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public Task IncrementAsync(Guid appId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(appId, Guid.Empty);

        return _mediator.Send(
            new IncrementCounterActivatorCommand
            {
                Name = CounterName,
                Counters = new Dictionary<string, long>
                {
                    [appId.ToString("N")] = 1,
                },
            },
            cancellationToken);
    }
}
