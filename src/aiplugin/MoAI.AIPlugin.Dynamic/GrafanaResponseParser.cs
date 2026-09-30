using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace MoAI.AIPlugin.Dynamic;

/// <summary>
/// Grafana 响应解析（纯函数，便于单测）：毫秒时间戳转 ISO、注解与仪表板列表解析.
/// </summary>
internal static class GrafanaResponseParser
{
    /// <summary>
    /// Grafana 毫秒时间戳转 ISO 8601 UTC.
    /// </summary>
    /// <param name="epochMs">毫秒时间戳（数字或字符串原文）.</param>
    /// <returns>ISO 8601 字符串；无法解析返回空串.</returns>
    public static string EpochMsToIso(string? epochMs)
    {
        if (!long.TryParse(epochMs, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms) || ms <= 0)
        {
            return string.Empty;
        }

        return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 解析注解列表.
    /// </summary>
    /// <param name="annotations">GET /api/annotations 响应数组.</param>
    /// <returns>注解列表.</returns>
    public static List<Models.GrafanaAnnotation> ParseAnnotations(JsonElement annotations)
    {
        var result = new List<Models.GrafanaAnnotation>();
        if (annotations.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in annotations.EnumerateArray())
        {
            result.Add(new Models.GrafanaAnnotation
            {
                Id = GetLongProperty(item, "id"),
                Text = ObservabilityJson.GetStringProperty(item, "text") ?? string.Empty,
                Tags = ReadStringArray(GetProperty(item, "tags")),
                TimeFrom = EpochMsToIso(GetRawProperty(item, "time")),
                TimeTo = EpochMsToIso(GetRawProperty(item, "timeEnd")),
                DashboardId = GetLongProperty(item, "dashboardId"),
                PanelId = GetLongProperty(item, "panelId"),
            });
        }

        return result;
    }

    /// <summary>
    /// 解析仪表板搜索结果.
    /// </summary>
    /// <param name="dashboards">GET /api/search 响应数组.</param>
    /// <returns>仪表板列表.</returns>
    public static List<Models.GrafanaDashboard> ParseDashboards(JsonElement dashboards)
    {
        var result = new List<Models.GrafanaDashboard>();
        if (dashboards.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in dashboards.EnumerateArray())
        {
            result.Add(new Models.GrafanaDashboard
            {
                Id = GetLongProperty(item, "id"),
                Uid = ObservabilityJson.GetStringProperty(item, "uid") ?? string.Empty,
                Title = ObservabilityJson.GetStringProperty(item, "title") ?? string.Empty,
                Url = ObservabilityJson.GetStringProperty(item, "url") ?? string.Empty,
                Type = ObservabilityJson.GetStringProperty(item, "type") ?? string.Empty,
            });
        }

        return result;
    }

    /// <summary>
    /// health 端点响应转键值字典.
    /// </summary>
    /// <param name="health">GET /api/health 响应对象.</param>
    /// <returns>键值字典.</returns>
    public static Dictionary<string, string> ParseHealth(JsonElement health)
    {
        return new Dictionary<string, string>(ObservabilityJson.ToStringMap(health));
    }

    private static JsonElement GetProperty(JsonElement parent, string name)
    {
        return parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value)
            ? value
            : default;
    }

    private static string? GetRawProperty(JsonElement parent, string name)
    {
        var value = GetProperty(parent, name);
        return value.ValueKind == JsonValueKind.Undefined
            ? null
            : value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
    }

    private static long GetLongProperty(JsonElement parent, string name)
    {
        var value = GetProperty(parent, name);
        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetInt64(out var number) ? number : 0,
            JsonValueKind.String => long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0,
            _ => 0,
        };
    }

    private static List<string> ReadStringArray(JsonElement array)
    {
        var result = new List<string>();
        if (array.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                result.Add(item.GetString() ?? string.Empty);
            }
        }

        return result;
    }
}
