using MediatR;
using MoAI.Hangfire.Services;

namespace MoAI.AIChannel.Services;

/// <summary>
/// AI 模型使用计数器默认实现.
/// </summary>
public class AiModelUsageCounter : IAiModelUsageCounter
{
    private const string CounterName = "ai-model-usage";
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiModelUsageCounter"/> class.
    /// </summary>
    /// <param name="mediator">消息中介器.</param>
    public AiModelUsageCounter(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public Task IncrementAsync(
        Guid modelId,
        int teamId,
        long userId,
        int useType,
        Guid useResourceId,
        int promptTokens,
        int completionTokens,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(teamId);
        ArgumentOutOfRangeException.ThrowIfNegative(userId);
        ArgumentOutOfRangeException.ThrowIfNegative(useType);
        ArgumentOutOfRangeException.ThrowIfNegative(promptTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(completionTokens);

        var dimension = new AiModelUsageCounterDimension(modelId, teamId, userId, useType, useResourceId);
        Dictionary<string, long> counters = new()
        {
            [AiModelUsageCounterKey.Create(dimension, AiModelUsageMetric.Count)] = 1,
        };

        AddPositive(counters, dimension, AiModelUsageMetric.Prompt, promptTokens);
        AddPositive(counters, dimension, AiModelUsageMetric.Completion, completionTokens);
        AddPositive(counters, dimension, AiModelUsageMetric.Total, (long)promptTokens + completionTokens);

        return _mediator.Send(
            new IncrementCounterActivatorCommand
            {
                Name = CounterName,
                Counters = counters,
            },
            cancellationToken);
    }

    private static void AddPositive(
        IDictionary<string, long> counters,
        AiModelUsageCounterDimension dimension,
        AiModelUsageMetric metric,
        long value)
    {
        if (value > 0)
        {
            counters[AiModelUsageCounterKey.Create(dimension, metric)] = value;
        }
    }
}