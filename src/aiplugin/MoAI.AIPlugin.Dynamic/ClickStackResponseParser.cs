using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace MoAI.AIPlugin.Dynamic;

/// <summary>
/// ClickStack（HyperDX）对外 API 响应解析（纯函数，便于单测）：sources 列表 / search 行集 / chart 时间线 / 错误归一 / 时间窗解析.
/// </summary>
internal static class ClickStackResponseParser
{
    /// <summary>chart 时间桶转 ISO 的毫秒精度格式.</summary>
    private const string IsoMillisFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    /// <summary>search 时间窗转 ISO 的秒精度格式.</summary>
    private const string IsoSecondsFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    /// <summary>
    /// 解析 GET api/v2/sources 响应（{ data: [source...] }）.
    /// </summary>
    /// <param name="root">响应根元素.</param>
    /// <returns>数据源列表；data 缺失或形态不符返回空列表.</returns>
    public static List<Models.ClickStackSource> ParseSources(JsonElement root)
    {
        var result = new List<Models.ClickStackSource>();
        var data = GetProperty(root, "data");
        if (data.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in data.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var from = GetProperty(item, "from");
            var table = ObservabilityJson.GetStringProperty(from, "tableName");
            if (string.IsNullOrEmpty(table))
            {
                table = FirstMetricTable(GetProperty(item, "metricTables"));
            }

            result.Add(new Models.ClickStackSource
            {
                Id = ObservabilityJson.GetStringProperty(item, "id") ?? string.Empty,
                Name = ObservabilityJson.GetStringProperty(item, "name") ?? string.Empty,
                Kind = ObservabilityJson.GetStringProperty(item, "kind") ?? string.Empty,
                Database = ObservabilityJson.GetStringProperty(from, "databaseName") ?? string.Empty,
                Table = table ?? string.Empty,
                DefaultSelect = ObservabilityJson.GetStringProperty(item, "defaultTableSelectExpression") ?? string.Empty,
                Disabled = GetBoolProperty(item, "disabled"),
            });
        }

        return result;
    }

    /// <summary>
    /// 解析 POST api/v2/search 响应的行集（{ data: [行对象...] }，每行为「列名 → 值」）.
    /// </summary>
    /// <param name="root">响应根元素.</param>
    /// <returns>行列表（值经 <see cref="ObservabilityJson.ToClr"/> 归一，NaN/+Inf 转文本）；data 缺失返回空列表.</returns>
    public static List<Dictionary<string, object?>> ParseSearchRows(JsonElement root)
    {
        var result = new List<Dictionary<string, object?>>();
        var data = GetProperty(root, "data");
        if (data.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var row in data.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var map = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var property in row.EnumerateObject())
            {
                map[property.Name] = ObservabilityJson.ToClr(property.Value);
            }

            result.Add(map);
        }

        return result;
    }

    /// <summary>
    /// 读取 search 响应的 rows 字段（本次返回行数），缺失时回退数据行数.
    /// </summary>
    /// <param name="root">响应根元素.</param>
    /// <param name="fallback">rows 字段缺失/非法时的回退值.</param>
    /// <returns>行数.</returns>
    public static int GetSearchRowCount(JsonElement root, int fallback)
    {
        var value = GetProperty(root, "rows");
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var rows) ? rows : fallback;
    }

    /// <summary>
    /// 解析 POST api/v2/charts/series 响应（{ data: [{ ts_bucket, "series_0.data", group }] }）.
    /// </summary>
    /// <param name="root">响应根元素.</param>
    /// <returns>时间线数据点；data 缺失或形态不符返回空列表.</returns>
    public static List<Models.ClickStackChartPoint> ParseChartPoints(JsonElement root)
    {
        var result = new List<Models.ClickStackChartPoint>();
        var data = GetProperty(root, "data");
        if (data.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in data.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            result.Add(new Models.ClickStackChartPoint
            {
                TsBucket = EpochMsToIso(GetLongProperty(item, "ts_bucket")),
                Value = GetChartValue(item),
                Group = ReadStringArray(GetProperty(item, "group")),
            });
        }

        return result;
    }

    /// <summary>
    /// 提取 API 错误响应的可读文本（search 形态 { message }，chart 形态 { error }）.
    /// </summary>
    /// <param name="root">错误响应根元素.</param>
    /// <returns>错误文本；无法提取返回 null.</returns>
    public static string? ExtractErrorMessage(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var message = ObservabilityJson.GetStringProperty(root, "message");
        if (!string.IsNullOrEmpty(message))
        {
            return message;
        }

        if (!root.TryGetProperty("error", out var error))
        {
            return null;
        }

        return error.ValueKind switch
        {
            JsonValueKind.String => error.GetString(),
            JsonValueKind.Object => ObservabilityJson.GetStringProperty(error, "message") ?? ObservabilityJson.Truncate(error.GetRawText(), 256),
            _ => null,
        };
    }

    /// <summary>
    /// 解析时间窗输入（ISO 8601 或 <c>yyyy-MM-dd HH:mm:ss</c> 等；无时区按 UTC 解释保证确定性）.
    /// </summary>
    /// <param name="input">用户输入原文.</param>
    /// <param name="time">解析结果.</param>
    /// <returns>是否解析成功；空串直接失败（由调用方决定缺省窗口）.</returns>
    public static bool TryParseTime(string? input, out DateTimeOffset time)
    {
        var trimmed = input?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            time = default;
            return false;
        }

        return DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out time);
    }

    /// <summary>
    /// 毫秒时间戳转 ISO 8601 UTC（毫秒精度）.
    /// </summary>
    /// <param name="epochMs">毫秒时间戳.</param>
    /// <returns>ISO 8601 字符串；非正数返回空串.</returns>
    public static string EpochMsToIso(long epochMs)
    {
        return epochMs <= 0
            ? string.Empty
            : DateTimeOffset.FromUnixTimeMilliseconds(epochMs).UtcDateTime.ToString(IsoMillisFormat, CultureInfo.InvariantCulture);
    }

    private static string? FirstMetricTable(JsonElement metricTables)
    {
        if (metricTables.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var table in metricTables.EnumerateObject())
        {
            var name = ObservabilityJson.GetStringProperty(metricTables, table.Name);
            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }
        }

        return null;
    }

    private static double? GetChartValue(JsonElement item)
    {
        foreach (var property in item.EnumerateObject())
        {
            if (property.Name.StartsWith("series_", StringComparison.Ordinal)
                && property.Name.EndsWith(".data", StringComparison.Ordinal)
                && property.Value.ValueKind == JsonValueKind.Number)
            {
                return property.Value.GetDouble();
            }
        }

        return null;
    }

    private static bool GetBoolProperty(JsonElement parent, string name)
    {
        var value = GetProperty(parent, name);
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.Number => value.GetDouble() != 0,
            JsonValueKind.String => bool.TryParse(value.GetString(), out var parsed) && parsed,
            _ => false,
        };
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
            result.Add(item.ValueKind switch
            {
                JsonValueKind.String => item.GetString() ?? string.Empty,
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => item.GetRawText(),
                JsonValueKind.Null => string.Empty,
                _ => ObservabilityJson.Truncate(item.GetRawText(), 256),
            });
        }

        return result;
    }

    private static JsonElement GetProperty(JsonElement parent, string name)
    {
        return parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value)
            ? value
            : default;
    }
}
