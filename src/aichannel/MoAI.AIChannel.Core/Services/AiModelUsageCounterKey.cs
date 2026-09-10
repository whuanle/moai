using System.Globalization;

namespace MoAI.AIChannel.Services;

internal enum AiModelUsageMetric
{
    Count,
    Prompt,
    Completion,
    Total,
}

internal readonly record struct AiModelUsageCounterDimension(
    Guid ModelId,
    int TeamId,
    long UserId,
    int UseType,
    Guid UseResourceId);

internal static class AiModelUsageCounterKey
{
    private const string Version = "v1";

    /// <summary>
    /// 创建模型用量计数键.
    /// </summary>
    /// <param name="dimension">统计维度.</param>
    /// <param name="metric">统计指标.</param>
    /// <returns>计数键.</returns>
    public static string Create(AiModelUsageCounterDimension dimension, AiModelUsageMetric metric)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Version}:{dimension.ModelId:N}:{dimension.TeamId}:{dimension.UserId}:{dimension.UseType}:{dimension.UseResourceId:N}:{ToValue(metric)}");
    }

    /// <summary>
    /// 解析模型用量计数键.
    /// </summary>
    /// <param name="value">计数键.</param>
    /// <param name="dimension">统计维度.</param>
    /// <param name="metric">统计指标.</param>
    /// <returns>是否解析成功.</returns>
    public static bool TryParse(string value, out AiModelUsageCounterDimension dimension, out AiModelUsageMetric metric)
    {
        dimension = default;
        metric = default;

        var parts = value.Split(':');
        if (parts.Length != 7
            || parts[0] != Version
            || !TryParseGuid(parts[1], out var modelId)
            || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var teamId)
            || teamId < 0
            || !long.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var userId)
            || userId < 0
            || !int.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out var useType)
            || useType < 0
            || !TryParseGuid(parts[5], out var useResourceId)
            || !TryParseMetric(parts[6], out metric))
        {
            return false;
        }

        dimension = new AiModelUsageCounterDimension(modelId, teamId, userId, useType, useResourceId);
        return true;
    }

    private static bool TryParseGuid(string value, out Guid result)
    {
        return Guid.TryParseExact(value, "N", out result)
            && value == result.ToString("N");
    }

    private static string ToValue(AiModelUsageMetric metric)
    {
        return metric switch
        {
            AiModelUsageMetric.Count => "count",
            AiModelUsageMetric.Prompt => "prompt",
            AiModelUsageMetric.Completion => "completion",
            AiModelUsageMetric.Total => "total",
            _ => throw new ArgumentOutOfRangeException(nameof(metric)),
        };
    }

    private static bool TryParseMetric(string value, out AiModelUsageMetric metric)
    {
        metric = value switch
        {
            "count" => AiModelUsageMetric.Count,
            "prompt" => AiModelUsageMetric.Prompt,
            "completion" => AiModelUsageMetric.Completion,
            "total" => AiModelUsageMetric.Total,
            _ => default,
        };

        return value is "count" or "prompt" or "completion" or "total";
    }
}