using System.Buffers;
using System.Text;
using System.Text.Json;

namespace MoAI.Infra.Helpers;

/// <summary>
/// Json 解析器.
/// </summary>
internal static class JsonParseHelper
{
    // 连接符，流程字段使用 `.` 连接
    private const char Connector = '.';
    private static readonly JsonSerializerOptions DefaultJsonSerializerOptions = new()
    {
        WriteIndented = true,
        MaxDepth = 10,
    };

    /// <summary>
    /// 从 json 读取生成字典.
    /// </summary>
    /// <param name="sequence"></param>
    /// <param name="readerOptions"></param>
    /// <returns></returns>
    public static Dictionary<string, object?> Read(ReadOnlySequence<byte> sequence, JsonReaderOptions readerOptions = default)
        => JsonToDictionaryConverter.Read(sequence, readerOptions);

    /// <summary>
    /// 从 json 读取生成字典.
    /// </summary>
    /// <param name="jsonElement"></param>
    /// <returns></returns>
    public static Dictionary<string, object?> Read(JsonElement jsonElement)
        => JsonToDictionaryConverter.Read(jsonElement);

    /// <summary>
    /// 从字典生成 json 字符串.
    /// </summary>
    /// <param name="dictionary"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static string Write(Dictionary<string, object?> dictionary, JsonSerializerOptions? options = null)
        => DictionaryToJsonConverter.Write(dictionary, options ?? DefaultJsonSerializerOptions);

    /// <summary>
    /// 写入 byte[].
    /// </summary>
    /// <param name="dictionary"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static byte[] WriteBytes(Dictionary<string, object?> dictionary, JsonSerializerOptions? options = null)
        => DictionaryToJsonConverter.WriteBytes(dictionary, options ?? DefaultJsonSerializerOptions);

    private static class JsonToDictionaryConverter
    {
        public static Dictionary<string, object?> Read(ReadOnlySequence<byte> sequence, JsonReaderOptions readerOptions)
        {
            var reader = new Utf8JsonReader(sequence, readerOptions);
            var map = new Dictionary<string, object?>(StringComparer.Ordinal);

            if (!reader.Read())
            {
                return map;
            }

            var pathBuilder = new PathBuilder();
            ReadToken(ref reader, map, ref pathBuilder);
            return map;
        }

        public static Dictionary<string, object?> Read(JsonElement element)
        {
            var map = new Dictionary<string, object?>(StringComparer.Ordinal);
            var pathBuilder = new PathBuilder();
            ReadElement(element, map, ref pathBuilder);
            return map;
        }

        private static void ReadToken(ref Utf8JsonReader reader, Dictionary<string, object?> map, ref PathBuilder pathBuilder)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    ReadObject(ref reader, map, ref pathBuilder);
                    break;
                case JsonTokenType.StartArray:
                    ReadArray(ref reader, map, ref pathBuilder);
                    break;
                default:
                    if (pathBuilder.IsEmpty)
                    {
                        return;
                    }

                    map[pathBuilder.Build()] = ReadValue(ref reader);
                    break;
            }
        }

        private static void ReadObject(ref Utf8JsonReader reader, Dictionary<string, object?> map, ref PathBuilder pathBuilder)
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                var propertyName = reader.GetString();
                if (propertyName is null)
                {
                    reader.Read();
                    continue;
                }

                reader.Read();
                pathBuilder.PushProperty(propertyName);
                try
                {
                    ReadToken(ref reader, map, ref pathBuilder);
                }
                finally
                {
                    pathBuilder.Pop();
                }
            }
        }

        private static void ReadArray(ref Utf8JsonReader reader, Dictionary<string, object?> map, ref PathBuilder pathBuilder)
        {
            var index = 0;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    return;
                }

                pathBuilder.PushArrayIndex(index);
                try
                {
                    ReadToken(ref reader, map, ref pathBuilder);
                }
                finally
                {
                    pathBuilder.Pop();
                }

                index++;
            }
        }

        private static object? ReadValue(ref Utf8JsonReader reader)
            => reader.TokenType switch
            {
                JsonTokenType.Null => null,
                JsonTokenType.False => false,
                JsonTokenType.True => true,
                JsonTokenType.Number => reader.GetDouble(),
                JsonTokenType.String => reader.GetString() ?? string.Empty,
                _ => null,
            };

        private static object? ReadValue(JsonElement element)
            => element.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.False => false,
                JsonValueKind.True => true,
                JsonValueKind.Number => element.GetDouble(),
                JsonValueKind.String => element.GetString() ?? string.Empty,
                _ => null,
            };

        private static void ReadElement(JsonElement element, Dictionary<string, object?> map, ref PathBuilder pathBuilder)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        pathBuilder.PushProperty(property.Name);
                        try
                        {
                            ReadElement(property.Value, map, ref pathBuilder);
                        }
                        finally
                        {
                            pathBuilder.Pop();
                        }
                    }

                    break;
                case JsonValueKind.Array:
                    var index = 0;
                    foreach (var arrayElement in element.EnumerateArray())
                    {
                        pathBuilder.PushArrayIndex(index);
                        try
                        {
                            ReadElement(arrayElement, map, ref pathBuilder);
                        }
                        finally
                        {
                            pathBuilder.Pop();
                        }

                        index++;
                    }

                    break;
                default:
                    if (!pathBuilder.IsEmpty)
                    {
                        map[pathBuilder.Build()] = ReadValue(element);
                    }

                    break;
            }
        }

        private sealed class PathBuilder
        {
            private readonly StringBuilder _builder = new(256);
            private readonly List<int> _lengthStack = new(16);

            public bool IsEmpty => _builder.Length == 0;

            public void PushProperty(string propertyName)
            {
                _lengthStack.Add(_builder.Length);
                if (_builder.Length > 0)
                {
                    _builder.Append(Connector);
                }

                _builder.Append(propertyName);
            }

            public void PushArrayIndex(int index)
            {
                _lengthStack.Add(_builder.Length);
                _builder.Append('[').Append(index).Append(']');
            }

            public void Pop()
            {
                if (_lengthStack.Count == 0)
                {
                    _builder.Clear();
                    return;
                }

                var previousLength = _lengthStack[^1];
                _lengthStack.RemoveAt(_lengthStack.Count - 1);
                _builder.Length = previousLength;
            }

            public string Build() => _builder.ToString();
        }
    }

    private static class DictionaryToJsonConverter
    {
        public static string Write(Dictionary<string, object?> dictionary, JsonSerializerOptions options)
        {
            var payload = BuildPayload(dictionary);
            return JsonSerializer.Serialize(payload, options);
        }

        public static byte[] WriteBytes(Dictionary<string, object?> dictionary, JsonSerializerOptions options)
        {
            var payload = BuildPayload(dictionary);
            return JsonSerializer.SerializeToUtf8Bytes(payload, options);
        }

        private static object BuildPayload(Dictionary<string, object?> values)
        {
            if (values.Count == 0)
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal);
            }

            var builder = new NestedStructureBuilder();
            foreach (var entry in values)
            {
                if (string.IsNullOrEmpty(entry.Key))
                {
                    continue;
                }

                using var reader = new PathStepReader(entry.Key);
                builder.Insert(reader.AsSpan(), entry.Value);
            }

            return builder.Build();
        }

        private sealed class NestedStructureBuilder
        {
            private object? _root;

            public void Insert(ReadOnlySpan<PathStep> steps, object? value)
            {
                if (steps.Length == 0)
                {
                    _root = value;
                    return;
                }

                _root ??= CreateContainer(steps[0]);
                InsertRecursive(_root, steps, 0, value);
            }

            public object Build()
                => _root ?? new Dictionary<string, object?>(StringComparer.Ordinal);

            private static void InsertRecursive(object container, ReadOnlySpan<PathStep> steps, int index, object? value)
            {
                var step = steps[index];
                var isLast = index == steps.Length - 1;

                if (step.IsProperty)
                {
                    var dictionary = EnsureDictionary(container);

                    if (isLast)
                    {
                        dictionary[step.PropertyName!] = value;
                        return;
                    }

                    var propertyNextStep = steps[index + 1];
                    if (!dictionary.TryGetValue(step.PropertyName!, out var next) || !IsCompatible(next, propertyNextStep))
                    {
                        next = CreateContainer(propertyNextStep);
                        dictionary[step.PropertyName!] = next;
                    }

                    InsertRecursive(next!, steps, index + 1, value);
                    return;
                }

                var list = EnsureList(container);
                var arrayIndex = step.ArrayIndex!.Value;

                while (list.Count <= arrayIndex)
                {
                    list.Add(null);
                }

                if (isLast)
                {
                    list[arrayIndex] = value;
                    return;
                }

                var arrayNextStep = steps[index + 1];
                var element = list[arrayIndex];
                if (element is null || !IsCompatible(element, arrayNextStep))
                {
                    element = CreateContainer(arrayNextStep);
                    list[arrayIndex] = element;
                }

                InsertRecursive(element!, steps, index + 1, value);
            }

            private static Dictionary<string, object?> EnsureDictionary(object container)
                => container is Dictionary<string, object?> dictionary
                    ? dictionary
                    : throw new InvalidOperationException("期望的容器为字典，但实际类型不匹配。");

            private static List<object?> EnsureList(object container)
                => container is List<object?> list
                    ? list
                    : throw new InvalidOperationException("期望的容器为数组，但实际类型不匹配。");

            private static object CreateContainer(PathStep step)
                => step.IsArray ? new List<object?>() : new Dictionary<string, object?>(StringComparer.Ordinal);

            private static bool IsCompatible(object? container, PathStep nextStep)
                => nextStep.IsArray ? container is List<object?> : container is Dictionary<string, object?>;
        }

        private sealed class PathStepReader : IDisposable
        {
            private readonly PathStepCollector _collector = new();

            public PathStepReader(string path)
            {
                Parse(path.AsSpan());
            }

            public ReadOnlySpan<PathStep> AsSpan() => _collector.AsSpan();

            public void Dispose() => _collector.Dispose();

            private void Parse(ReadOnlySpan<char> span)
            {
                var position = 0;

                while (position < span.Length)
                {
                    var current = span[position];
                    if (current == Connector)
                    {
                        position++;
                        continue;
                    }

                    if (current == '[')
                    {
                        var closingOffset = span.Slice(position + 1).IndexOf(']');
                        if (closingOffset == -1)
                        {
                            break;
                        }

                        var numberSpan = span.Slice(position + 1, closingOffset);
                        _collector.Append(PathStep.Array(ParseNumber(numberSpan)));
                        position += closingOffset + 2;
                        continue;
                    }

                    var start = position;
                    while (position < span.Length && span[position] != ':' && span[position] != '[')
                    {
                        position++;
                    }

                    if (position > start)
                    {
                        var propertyName = span.Slice(start, position - start).ToString();
                        if (!string.IsNullOrEmpty(propertyName))
                        {
                            _collector.Append(PathStep.Property(propertyName));
                        }
                    }

                    while (position < span.Length && span[position] == '[')
                    {
                        var closingOffset = span.Slice(position + 1).IndexOf(']');
                        if (closingOffset == -1)
                        {
                            position = span.Length;
                            break;
                        }

                        var numberSpan = span.Slice(position + 1, closingOffset);
                        _collector.Append(PathStep.Array(ParseNumber(numberSpan)));
                        position += closingOffset + 2;
                    }
                }
            }

            private static int ParseNumber(ReadOnlySpan<char> span)
            {
                var result = 0;
                foreach (var ch in span)
                {
                    if (ch < '0' || ch > '9')
                    {
                        break;
                    }

                    result = (result * 10) + (ch - '0');
                }

                return result;
            }
        }

        private sealed class PathStepCollector : IDisposable
        {
            private const int DefaultCapacity = 16;
            private PathStep[]? _buffer;
            private int _count;

            public void Append(PathStep step)
            {
                var buffer = _buffer;
                if (buffer is null)
                {
                    buffer = ArrayPool<PathStep>.Shared.Rent(DefaultCapacity);
                    _buffer = buffer;
                }
                else if (_count == buffer.Length)
                {
                    var next = ArrayPool<PathStep>.Shared.Rent(buffer.Length * 2);
                    Array.Copy(buffer, 0, next, 0, _count);
                    ArrayPool<PathStep>.Shared.Return(buffer, clearArray: true);
                    buffer = next;
                    _buffer = buffer;
                }

                buffer[_count++] = step;
            }

            public ReadOnlySpan<PathStep> AsSpan()
                => _buffer is null ? ReadOnlySpan<PathStep>.Empty : _buffer.AsSpan(0, _count);

            public void Dispose()
            {
                if (_buffer is not null)
                {
                    ArrayPool<PathStep>.Shared.Return(_buffer, clearArray: true);
                    _buffer = null;
                    _count = 0;
                }
            }
        }

        private readonly struct PathStep
        {
            public string? PropertyName { get; }

            public int? ArrayIndex { get; }

            public bool IsProperty => PropertyName is not null;

            public bool IsArray => ArrayIndex.HasValue;

            private PathStep(string? propertyName, int? arrayIndex)
            {
                PropertyName = propertyName;
                ArrayIndex = arrayIndex;
            }

            public static PathStep Property(string propertyName) => new(propertyName, null);

            public static PathStep Array(int index) => new(null, index);
        }
    }
}