using System;
using System.Globalization;

namespace MoAI.AIPlugin.Dynamic;

/// <summary>
/// Loki 时间参数归一（纯函数，便于单测）：RFC3339 / Unix 秒 / 毫秒 → Loki 使用的 Unix 纳秒字符串.
/// </summary>
internal static class LokiTimeHelper
{
    /// <summary>
    /// 归一时间参数.
    /// </summary>
    /// <param name="value">RFC3339 文本、Unix 秒（≤10 位）、毫秒（11-13 位）或纳秒原文（&gt;13 位）.</param>
    /// <returns>Unix 纳秒字符串；无法解析返回 null.</returns>
    public static string? ToNanoSeconds(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var time))
        {
            return (time.ToUnixTimeMilliseconds() * 1_000_000L).ToString(CultureInfo.InvariantCulture);
        }

        if (!long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
        {
            return null;
        }

        var nanoSeconds = trimmed.Length switch
        {
            <= 10 => numeric * 1_000_000_000L,
            <= 13 => numeric * 1_000_000L,
            <= 16 => numeric * 1_000L,
            _ => numeric,
        };
        return nanoSeconds.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Loki 纳秒时间戳转 ISO 8601 UTC.
    /// </summary>
    /// <param name="nanoSeconds">纳秒原文.</param>
    /// <returns>ISO 8601 字符串；无法解析返回空串.</returns>
    public static string NanoSecondsToIso(string? nanoSeconds)
    {
        if (!long.TryParse(nanoSeconds, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ns) || ns <= 0)
        {
            return string.Empty;
        }

        return DateTimeOffset.FromUnixTimeMilliseconds(ns / 1_000_000L)
            .UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }
}
