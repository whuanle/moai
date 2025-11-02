using System.Buffers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MoAI.Infra.Helpers;

// todo: 后续优化性能

/// <summary>
/// JSON 读取帮助类.
/// </summary>
internal static class JsonParseHelper
{
    private static readonly JsonSerializerOptions DefaultJsonSerializerOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        MaxDepth = 10, // 设置最大深度，防止过深的嵌套导致栈溢出
    };

    /// <summary>
    /// 读取 json 字节流.
    /// </summary>
    /// <param name="sequence"></param>
    /// <param name="jsonReaderOptions"></param>
    /// <returns>字典集合.</returns>
    public static Dictionary<string, object> Read(ReadOnlySequence<byte> sequence, JsonReaderOptions jsonReaderOptions)
    {
        var reader = new Utf8JsonReader(sequence, jsonReaderOptions);
        var map = new Dictionary<string, object>();
        BuildJsonField(ref reader, map, null);
        return map;
    }

    /// <summary>
    /// 将 Dictionary 转换为 JSON 字符串.
    /// </summary>
    /// <param name="dictionary">需要转换的字典.</param>
    /// <param name="options">JSON 序列化选项.</param>
    /// <returns>JSON 字符串.</returns>
    public static string Write(Dictionary<string, object> dictionary, JsonSerializerOptions? options = null)
    {
        var convertedDict = ConvertDictionaryForSerialization(dictionary);
        return JsonSerializer.Serialize(convertedDict, DefaultJsonSerializerOptions);
    }

    /// <summary>
    /// 将 Dictionary 转换为 JSON 字节数组.
    /// </summary>
    /// <param name="dictionary">需要转换的字典.</param>
    /// <param name="options">JSON 序列化选项.</param>
    /// <returns>JSON 字节数组.</returns>
    public static byte[] WriteBytes(Dictionary<string, object> dictionary, JsonSerializerOptions? options = null)
    {
        var convertedDict = ConvertDictionaryForSerialization(dictionary);
        return JsonSerializer.SerializeToUtf8Bytes(convertedDict, DefaultJsonSerializerOptions);
    }

    // 处理嵌套键格式（例如 "parent.child"）和数组键（例如 "key[0]"）
    private static object ConvertDictionaryForSerialization(Dictionary<string, object> dictionary)
    {
        var result = new Dictionary<string, object?>();

        foreach (var pair in dictionary)
        {
            BuildNestedObject(result, pair.Key, pair.Value);
        }

        return result;
    }

    private static void BuildNestedObject(Dictionary<string, object?> result, string key, object value)
    {
        // 处理数组格式 "key[0]"
        if (key.Contains('[') && key.EndsWith(']'))
        {
            var arrayMatch = Regex.Match(key, @"^(.*?)\[(\d+)\]$");
            if (arrayMatch.Success)
            {
                var arrayKey = arrayMatch.Groups[1].Value;
                var index = int.Parse(arrayMatch.Groups[2].Value);

                // 如果是嵌套数组路径
                if (arrayKey.Contains('.'))
                {
                    var parts = arrayKey.Split('.', 2);
                    var parent = parts[0];
                    var remainingPath = $"{parts[1]}[{index}]";

                    if (!result.TryGetValue(parent, out var parentValue) || parentValue is not Dictionary<string, object?>)
                    {
                        result[parent] = new Dictionary<string, object?>();
                    }

                    BuildNestedObject((Dictionary<string, object?>)result[parent]!, remainingPath, value);
                }
                else
                {
                    // 直接数组处理
                    if (!result.TryGetValue(arrayKey, out var existingArray) || existingArray is not List<object?>)
                    {
                        result[arrayKey] = new List<object?>();
                    }

                    var array = (List<object?>)result[arrayKey]!;

                    // 确保数组长度足够
                    while (array.Count <= index)
                    {
                        array.Add(null);
                    }

                    array[index] = value;
                }

                return;
            }
        }

        // 处理嵌套键格式 "parent:child"
        if (key.Contains('.'))
        {
            var parts = key.Split('.', 2);
            var parent = parts[0];
            var child = parts[1];

            if (!result.TryGetValue(parent, out var parentValue) || parentValue is not Dictionary<string, object?>)
            {
                result[parent] = new Dictionary<string, object?>();
            }

            BuildNestedObject((Dictionary<string, object?>)result[parent]!, child, value);
        }
        else
        {
            // 常规键
            result[key] = value;
        }
    }

    // 解析 json 对象
    private static void BuildJsonField(ref Utf8JsonReader reader, Dictionary<string, object> map, string? baseKey)
    {
        while (reader.Read())
        {
            // 顶级数组 "[123,123]"
            if (reader.TokenType is JsonTokenType.StartArray)
            {
                ParseArray(ref reader, map, baseKey);
            }
            else if (reader.TokenType is JsonTokenType.EndObject)
            {
                break;
            }
            else if (reader.TokenType is JsonTokenType.PropertyName)
            {
                var key = reader.GetString()!;
                var newkey = baseKey is null ? key : $"{baseKey}:{key}";

                reader.Read();
                if (reader.TokenType is JsonTokenType.StartArray)
                {
                    ParseArray(ref reader, map, newkey);
                }
                else if (reader.TokenType is JsonTokenType.StartObject)
                {
                    BuildJsonField(ref reader, map, newkey);
                }
                else
                {
                    map[newkey] = ReadObject(ref reader) ?? string.Empty;
                }
            }
        }
    }

    // 解析数组
    private static void ParseArray(ref Utf8JsonReader reader, Dictionary<string, object> map, string? baseKey)
    {
        int i = 0;
        while (reader.Read())
        {
            if (reader.TokenType is JsonTokenType.EndArray)
            {
                break;
            }

            var newkey = baseKey is null ? $"[{i}]" : $"{baseKey}[{i}]";
            i++;

            switch (reader.TokenType)
            {
                // [...,null,...]
                case JsonTokenType.Null:
                    map[newkey] = default!;
                    break;

                // [...,123.666,...]
                case JsonTokenType.Number:
                    map[newkey] = reader.GetDouble();
                    break;

                // [...,"123",...]
                case JsonTokenType.String:
                    map[newkey] = reader.GetString()!;
                    break;

                // [...,true,...]
                case JsonTokenType.True:
                    map[newkey] = reader.GetBoolean();
                    break;

                case JsonTokenType.False:
                    map[newkey] = reader.GetBoolean();
                    break;

                // [...,{...},...]
                case JsonTokenType.StartObject:
                    BuildJsonField(ref reader, map, newkey);
                    break;

                // [...,[],...]
                case JsonTokenType.StartArray:
                    ParseArray(ref reader, map, newkey);
                    break;
                default:
                    map[newkey] = JsonValueKind.Null;
                    break;
            }
        }
    }

    // 读取字段值
    private static object? ReadObject(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null or JsonTokenType.None:
                return null;
            case JsonTokenType.False:
                return reader.GetBoolean();
            case JsonTokenType.True:
                return reader.GetBoolean();
            case JsonTokenType.Number:
                return reader.GetDouble();
            case JsonTokenType.String:
                return reader.GetString() ?? string.Empty;
            default: return null;
        }
    }
}