using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace MoAI.AIPlugin.Dynamic;

/// <summary>
/// 智能运维观测插件共用的 JSON 容错展开工具：把上游各服务返回的 JSON（Prometheus 响应 / _cat 行 / JSONEachRow 行 / Tempo 链路）安全归一为可序列化结构.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description><see cref="ToClr"/> 把 <see cref="JsonElement"/> 递归转为基础 CLR 值；非有限数字（NaN/+Inf/-Inf）转字符串，避免 <see cref="JsonSerializer"/> 序列化抛异常。</description></item>
/// <item><description><see cref="ToStringMap"/> 把标签/属性对象转平的「键 → 字符串」映射，无法理解的片段截断后回写原文 JSON，保证模型可读。</description></item>
/// <item><description>按编写规范要求：字段缺失/形态变化/空对象都要能跳过而不是抛异常。</description></item>
/// </list>
/// </remarks>
internal static class ObservabilityJson
{
    /// <summary>标签/属性值为 object/array 时回写原文的截断长度.</summary>
    private const int MaxRawTextChars = 512;

    /// <summary>截断尾部标记（必须短于可配置的最小截断长度）.</summary>
    private const string TruncationMarker = "…";

    /// <summary>
    /// 在父对象里读取指定属性并归一为字符串.
    /// </summary>
    /// <param name="parent">父对象元素.</param>
    /// <param name="propertyName">属性名.</param>
    /// <returns>字符串/数字/布尔 → 文本；属性缺失或为 null/对象/数组 → null.</returns>
    public static string? GetStringProperty(JsonElement parent, string propertyName)
    {
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
            _ => null,
        };
    }

    /// <summary>
    /// 把标签/属性对象转平的「键 → 字符串」映射：数字/布尔取原文，null 归空串；object/array 属性被丢弃.
    /// </summary>
    /// <param name="element">标签/属性对象（可为任意 ValueKind，非法输入返回空映射）.</param>
    /// <returns>键值映射.</returns>
    public static IReadOnlyDictionary<string, string> ToStringMap(JsonElement element)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (element.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!property.Value.ValueKind.IsScalarKind())
            {
                continue;
            }

            var text = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                JsonValueKind.Null => string.Empty,
                _ => Truncate(property.Value.GetRawText(), MaxRawTextChars),
            };

            result[property.Name] = text;
        }

        return result;
    }

    /// <summary>
    /// 把 OTel JSON 形态的属性（<c>[{key, value:{stringValue|intVal...}}]</c> 数组）或平对象归一为「键 → 字符串」映射.
    /// </summary>
    /// <param name="element">属性节点.</param>
    /// <returns>键值映射；OTel 属性取不到 value 时丢弃该条.</returns>
    public static IReadOnlyDictionary<string, string> ToOtelMap(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var item in element.EnumerateArray())
            {
                var key = GetStringProperty(item, "key");
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (TryGetOtelValue(item, out var text))
                {
                    result[key] = text;
                }
            }

            return result;
        }

        return ToStringMap(element);
    }

    /// <summary>
    /// 把 <see cref="JsonElement"/> 递归归一为基础 CLR 值.
    /// </summary>
    /// <param name="element">源元素.</param>
    /// <returns>归一后的值.</returns>
    public static object? ToClr(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => ToClrObject(element),
            JsonValueKind.Array => ToClrList(element),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => ToClrNumber(element),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    /// <summary>
    /// 解析上游响应 JSON：非法且非空文本时抛可读业务异常.
    /// </summary>
    /// <param name="raw">响应文本.</param>
    /// <param name="serviceName">服务名（用于错误提示）.</param>
    /// <returns>解析后的文档（调用方负责释放）.</returns>
    /// <exception cref="MoAI.Infra.Exceptions.BusinessException">响应非合法 JSON.</exception>
    public static JsonDocument ParseOrThrow(string raw, string serviceName)
    {
        try
        {
            return JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
        }
        catch (JsonException)
        {
            throw new MoAI.Infra.Exceptions.BusinessException(502, $"{serviceName} 响应不是合法 JSON，请检查服务地址是否正确");
        }
    }

    /// <summary>
    /// 截断文本.
    /// </summary>
    /// <param name="text">原文.</param>
    /// <param name="maxChars">最大字符数.</param>
    /// <returns>截断后的文本.</returns>
    public static string Truncate(string text, int maxChars)
    {
        if (maxChars <= 0)
        {
            return string.Empty;
        }

        return text.Length <= maxChars
            ? text
            : $"{text.Substring(0, maxChars - TruncationMarker.Length)}{TruncationMarker}";
    }

    /// <summary>
    /// 从 OTel 属性条目里取可读字符串值（兼容 stringValue/intValue/doubleValue/boolValue 及数组/嵌套形态）.
    /// </summary>
    /// <param name="item">属性条目.</param>
    /// <param name="text">归一后的文本.</param>
    /// <returns>取到有效值时为 true.</returns>
    private static bool TryGetOtelValue(JsonElement item, out string text)
    {
        text = string.Empty;
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("value", out var value))
        {
            return false;
        }

        if (value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
        {
            text = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText();
            return true;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var optionKind in new[] { "stringValue", "intValue", "doubleValue", "boolValue", "bytesValue" })
        {
            if (value.TryGetProperty(optionKind, out var option) && option.ValueKind.IsScalarKind())
            {
                text = option.ValueKind == JsonValueKind.String ? option.GetString() ?? string.Empty : option.GetRawText();
                return true;
            }
        }

        // array/kvlist 等复杂形态截断回写原文，保证模型可读而不是丢弃
        text = Truncate(value.GetRawText(), MaxRawTextChars);
        return true;
    }

    private static Dictionary<string, object?> ToClrObject(JsonElement element)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            result[property.Name] = ToClr(property.Value);
        }

        return result;
    }

    private static List<object?> ToClrList(JsonElement element)
    {
        var result = new List<object?>();
        foreach (var item in element.EnumerateArray())
        {
            result.Add(ToClr(item));
        }

        return result;
    }

    private static object ToClrNumber(JsonElement element)
    {
        if (element.TryGetInt64(out var longValue))
        {
            return longValue;
        }

        var doubleValue = element.GetDouble();
        if (!double.IsFinite(doubleValue))
        {
            return doubleValue.ToString(CultureInfo.InvariantCulture).ToUpperInvariant();
        }

        return doubleValue;
    }

    private static bool IsScalarKind(this JsonValueKind valueKind)
    {
        return valueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null;
    }
}
