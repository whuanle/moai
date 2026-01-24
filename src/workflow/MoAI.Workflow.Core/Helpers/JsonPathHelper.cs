#pragma warning disable CA1031 // 不捕获常规异常类型

using Json.Path;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MoAI.Workflow.Helpers;

/// <summary>
/// JSONPath 提取工具类，支持从JSON文本或JsonElement中提取多个JSONPath对应的值
/// </summary>
public static class JsonPathHelper
{
    /// <summary>
    /// 从JSON文本中提取多个JSONPath对应的值（返回.NET原生类型）
    /// </summary>
    /// <param name="jsonText">JSON文本</param>
    /// <param name="jsonPaths">多个JSONPath表达式</param>
    /// <returns>键为JSONPath，值为对应提取结果（.NET原生类型）的字典</returns>
    /// <exception cref="ArgumentNullException">JSON文本或JSONPath为空时抛出</exception>
    public static Dictionary<string, List<object?>> ExtractFromJsonText(string jsonText, IEnumerable<string> jsonPaths)
    {
        JsonNode? jsonNode = JsonNode.Parse(jsonText);
        if (jsonNode == null)
        {
            return new Dictionary<string, List<object?>>();
        }

        return ExtractFromJsonNode(jsonNode, jsonPaths);
    }

    /// <summary>
    /// 从JsonElement中提取多个JSONPath对应的值（返回.NET原生类型）
    /// </summary>
    /// <param name="jsonElement">JsonElement对象</param>
    /// <param name="jsonPaths">多个JSONPath表达式</param>
    /// <returns>键为JSONPath，值为对应提取结果（.NET原生类型）的字典</returns>
    /// <exception cref="ArgumentNullException">JSONPath为空时抛出</exception>
    public static Dictionary<string, List<object?>> ExtractFromJsonElement(JsonElement jsonElement, IEnumerable<string> jsonPaths)
    {
        JsonNode? jsonNode = JsonNode.Parse(jsonElement.GetRawText());
        if (jsonNode == null)
        {
            return new Dictionary<string, List<object?>>();
        }

        return ExtractFromJsonNode(jsonNode, jsonPaths);
    }

    /// <summary>
    /// 内部通用方法：从JsonNode中提取多个JSONPath的值并转换为.NET原生类型
    /// </summary>
    /// <param name="jsonNode">JsonNode对象</param>
    /// <param name="jsonPaths">多个JSONPath表达式</param>
    /// <returns>提取结果字典（值为.NET原生类型）</returns>
    private static Dictionary<string, List<object?>> ExtractFromJsonNode(JsonNode jsonNode, IEnumerable<string> jsonPaths)
    {
        var resultDict = new Dictionary<string, List<object?>>();

        foreach (var pathStr in jsonPaths)
        {
            try
            {
                // 解析JSONPath表达式
                var jsonPath = JsonPath.Parse(pathStr);

                // 执行查询
                var evaluationResult = jsonPath.Evaluate(jsonNode);

                // 提取匹配结果并转换为.NET原生类型
                var matches = evaluationResult.Matches
                    .Select(match => ConvertJsonNodeToNativeType(match.Value))
                    .ToList();

                // 添加到结果字典
                resultDict[pathStr] = matches;
            }
            catch
            {
                resultDict[pathStr] = new List<object?>();
            }
        }

        return resultDict;
    }

    private static object? ConvertJsonNodeToNativeType(JsonNode? node)
    {
        if (node == null)
        {
            return null;
        }

        return node switch
        {
            JsonValue value when value.GetValueKind() == JsonValueKind.String => value.GetValue<string>(),
            JsonValue value when value.GetValueKind() == JsonValueKind.True => true,
            JsonValue value when value.GetValueKind() == JsonValueKind.False => false,
            JsonValue value when value.GetValueKind() == JsonValueKind.Number =>
                value.TryGetValue<int>(out int intVal) ? intVal :
                value.TryGetValue<double>(out double doubleVal) ? doubleVal :
                value.GetValue<decimal>(),
            JsonArray array => array.Select(ConvertJsonNodeToNativeType).ToList(),
            JsonObject obj => obj.ToDictionary(
                kvp => kvp.Key,
                kvp => ConvertJsonNodeToNativeType(kvp.Value)),
            JsonValue value when value.GetValueKind() == JsonValueKind.Null => null,
            _ => node.ToString()
        };
    }
}
