using System.Globalization;
using System.Text.Json;

namespace MoAI.Subsystem.Client.Events;

/// <summary>
/// 事件载荷读取扩展：统一处理 JSON 数字/字符串双形态 ID、数组与 <c>truncated</c> 降级标记，
/// 避免接入方在各处手写 <see cref="JsonElement"/> 解析。
/// </summary>
public static class SubsysEventItemExtensions
{
    /// <summary>是否服务端控制事件 <c>subsys.resync_required</c>.</summary>
    /// <param name="item">事件.</param>
    /// <returns>控制事件返回 true.</returns>
    public static bool IsResyncRequired(this SubsysEventItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return string.Equals(item.Event, SubsysEventNames.ResyncRequired, StringComparison.Ordinal);
    }

    /// <summary>读取 <c>data.reason</c>（控制事件的对账原因）.</summary>
    /// <param name="item">事件.</param>
    /// <returns>原因：initial_sync/cursor_expired/overflow/replay_unavailable；缺失返回 null.</returns>
    public static string? GetResyncReason(this SubsysEventItem item) => item.GetDataString("reason");

    /// <summary>载荷是否被服务端截断（关联 ID 数组超过 50 个时退化为计数）.</summary>
    /// <param name="item">事件.</param>
    /// <returns>被截断返回 true.</returns>
    public static bool IsTruncated(this SubsysEventItem item) => item.GetDataBool("truncated") == true;

    /// <summary>读取字符串字段（数字/布尔自动转字符串）.</summary>
    /// <param name="item">事件.</param>
    /// <param name="propertyName">字段名.</param>
    /// <returns>字符串值；缺失/null 返回 null.</returns>
    public static string? GetDataString(this SubsysEventItem item, string propertyName)
    {
        var value = GetDataElement(item, propertyName);
        if (value is null)
        {
            return null;
        }

        return value.Value.ValueKind switch
        {
            JsonValueKind.String => value.Value.GetString(),
            JsonValueKind.Number => value.Value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
    }

    /// <summary>读取 long 字段（服务端 ID 以字符串传输，兼容数字形态）.</summary>
    /// <param name="item">事件.</param>
    /// <param name="propertyName">字段名.</param>
    /// <returns>long 值；缺失/非法返回 null.</returns>
    public static long? GetDataInt64(this SubsysEventItem item, string propertyName)
    {
        var value = GetDataElement(item, propertyName);
        if (value is null)
        {
            return null;
        }

        if (value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetInt64(out var number))
        {
            return number;
        }

        return value.Value.ValueKind == JsonValueKind.String
            && long.TryParse(value.Value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    /// <summary>读取 int 字段.</summary>
    /// <param name="item">事件.</param>
    /// <param name="propertyName">字段名.</param>
    /// <returns>int 值；缺失/非法返回 null.</returns>
    public static int? GetDataInt32(this SubsysEventItem item, string propertyName)
    {
        var value = item.GetDataInt64(propertyName);
        return value is >= int.MinValue and <= int.MaxValue ? (int)value.Value : null;
    }

    /// <summary>读取 bool 字段.</summary>
    /// <param name="item">事件.</param>
    /// <param name="propertyName">字段名.</param>
    /// <returns>bool 值；缺失/非法返回 null.</returns>
    public static bool? GetDataBool(this SubsysEventItem item, string propertyName)
    {
        var value = GetDataElement(item, propertyName);
        if (value is null)
        {
            return null;
        }

        return value.Value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(value.Value.GetString(), out var parsed) ? parsed : null,
            _ => null,
        };
    }

    /// <summary>读取字符串数组（数字元素自动转字符串；非数组返回空集合）.</summary>
    /// <param name="item">事件.</param>
    /// <param name="propertyName">字段名.</param>
    /// <returns>字符串集合；缺失返回空集合.</returns>
    public static IReadOnlyList<string> GetDataStrings(this SubsysEventItem item, string propertyName)
    {
        var value = GetDataElement(item, propertyName);
        if (value is null || value.Value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<string>(value.Value.GetArrayLength());
        foreach (var element in value.Value.EnumerateArray())
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    var text = element.GetString();
                    if (text is not null)
                    {
                        result.Add(text);
                    }

                    break;
                case JsonValueKind.Number:
                    result.Add(element.GetRawText());
                    break;
            }
        }

        return result;
    }

    /// <summary>载荷是否包含指定字段.</summary>
    /// <param name="item">事件.</param>
    /// <param name="propertyName">字段名.</param>
    /// <returns>包含返回 true.</returns>
    public static bool HasDataProperty(this SubsysEventItem item, string propertyName) => GetDataElement(item, propertyName) is not null;

    private static JsonElement? GetDataElement(SubsysEventItem item, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        if (item.Data is not JsonElement data
            || data.ValueKind != JsonValueKind.Object
            || !data.TryGetProperty(propertyName, out var value)
            || value.ValueKind == JsonValueKind.Null
            || value.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        return value;
    }
}
