using MediatR;
using MoAI.Hangfire.Services;

namespace MoAI.AIPlugin.Services;

/// <summary>
/// 插件使用次数计数器默认实现.
/// </summary>
public class PluginUsageCounter : IPluginUsageCounter
{
    private const string CounterName = "plugin-usage";
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginUsageCounter"/> class.
    /// </summary>
    /// <param name="mediator">消息中介器.</param>
    public PluginUsageCounter(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public Task IncrementAsync(Guid pluginId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(pluginId, Guid.Empty);

        return _mediator.Send(
            new IncrementCounterActivatorCommand
            {
                Name = CounterName,
                Counters = new Dictionary<string, long>
                {
                    [pluginId.ToString("N")] = 1,
                },
            },
            cancellationToken);
    }
}