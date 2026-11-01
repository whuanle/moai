using MoAI.Statistics.Services;

namespace MoAI.AIChannel.Services;

/// <summary>
/// AI 模型使用计数器默认实现：委托统计模块领域服务批量推送 Redis.
/// </summary>
public class AiModelUsageCounter : IAiModelUsageCounter
{
    private readonly IResourceUsageService _resourceUsageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiModelUsageCounter"/> class.
    /// </summary>
    /// <param name="resourceUsageService">资源使用统计领域服务.</param>
    public AiModelUsageCounter(IResourceUsageService resourceUsageService)
    {
        _resourceUsageService = resourceUsageService;
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
        int cachedTokens = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(teamId);
        ArgumentOutOfRangeException.ThrowIfNegative(userId);
        ArgumentOutOfRangeException.ThrowIfNegative(useType);
        ArgumentOutOfRangeException.ThrowIfNegative(promptTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(completionTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(cachedTokens);

        var dimension = new AiModelUsageCounterDimension(modelId, teamId, userId, useType, useResourceId);
        Dictionary<string, long> counters = new()
        {
            [AiModelUsageCounterKey.Create(dimension, AiModelUsageMetric.Count)] = 1,
        };

        AddPositive(counters, dimension, AiModelUsageMetric.Prompt, promptTokens);
        AddPositive(counters, dimension, AiModelUsageMetric.Completion, completionTokens);
        AddPositive(counters, dimension, AiModelUsageMetric.Total, (long)promptTokens + completionTokens);
        AddPositive(counters, dimension, AiModelUsageMetric.Cached, cachedTokens);

        return _resourceUsageService.RecordAsync(ResourceUsageNames.AiModel, counters, cancellationToken);
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