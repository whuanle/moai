using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace MoAI.AIPlugin.Dynamic;

/// <summary>
/// Zabbix JSON-RPC 响应解析（纯函数，便于单测）：错误归一、严重级映射、时间与持续时长的人类可读化.
/// </summary>
internal static class ZabbixResponseParser
{
    /// <summary>
    /// 严重级 0-5 的中文名.
    /// </summary>
    private static readonly string[] SeverityNames = { "未分类", "信息", "警告", "一般", "严重", "灾难" };

    /// <summary>
    /// 严重级数值转中文名.
    /// </summary>
    /// <param name="severity">严重级 0-5.</param>
    /// <returns>严重级名称；越界返回「未知」.</returns>
    public static string SeverityName(int severity)
    {
        return severity >= 0 && severity < SeverityNames.Length ? SeverityNames[severity] : "未知";
    }

    /// <summary>
    /// 提取 JSON-RPC error 对象并归一为一段可读文本.
    /// </summary>
    /// <param name="root">JSON-RPC 响应根元素.</param>
    /// <returns>错误文本；无 error 字段返回 null.</returns>
    public static string? ExtractError(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var code = ObservabilityJson.GetStringProperty(error, "code") ?? string.Empty;
        var message = ObservabilityJson.GetStringProperty(error, "message") ?? string.Empty;
        var data = ObservabilityJson.GetStringProperty(error, "data") ?? string.Empty;
        return $"({code}) {message}".Trim() + (string.IsNullOrEmpty(data) ? string.Empty : $"：{data}");
    }

    /// <summary>
    /// Zabbix clock（Unix 秒）转 ISO 8601 UTC 字符串.
    /// </summary>
    /// <param name="seconds">Unix 秒（字符串或数字原文）.</param>
    /// <returns>ISO 8601 字符串；无法解析返回空串.</returns>
    public static string ClockToIso(string? seconds)
    {
        if (!long.TryParse(seconds, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixSeconds) || unixSeconds <= 0)
        {
            return string.Empty;
        }

        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 由 clock（Unix 秒）计算已持续时长（人读格式）.
    /// </summary>
    /// <param name="seconds">Unix 秒（字符串或数字原文）.</param>
    /// <returns>如 3d 4h 12m；无法解析或在未来返回空串.</returns>
    public static string AgeFromClock(string? seconds)
    {
        if (!long.TryParse(seconds, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixSeconds) || unixSeconds <= 0)
        {
            return string.Empty;
        }

        var totalSeconds = (DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(unixSeconds)).TotalSeconds;
        if (totalSeconds < 0)
        {
            return string.Empty;
        }

        var days = (int)(totalSeconds / 86400);
        var hours = (int)(totalSeconds % 86400 / 3600);
        var minutes = (int)(totalSeconds % 3600 / 60);
        var parts = new List<string>();
        if (days > 0)
        {
            parts.Add($"{days}d");
        }

        if (hours > 0)
        {
            parts.Add($"{hours}h");
        }

        if (minutes > 0)
        {
            parts.Add($"{minutes}m");
        }

        return parts.Count == 0 ? "<1m" : string.Join(" ", parts);
    }

    /// <summary>
    /// 解析 problem.get selectTags 的标签数组为键值字典.
    /// </summary>
    /// <param name="tags">tags 数组元素（{tag, value}）.</param>
    /// <returns>标签字典（缺 value 以空串占位，重复 tag 后者覆盖）.</returns>
    public static Dictionary<string, string> ParseTags(JsonElement tags)
    {
        var result = new Dictionary<string, string>();
        if (tags.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var tag in tags.EnumerateArray())
        {
            var name = ObservabilityJson.GetStringProperty(tag, "tag");
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            result[name] = ObservabilityJson.GetStringProperty(tag, "value") ?? string.Empty;
        }

        return result;
    }
}
