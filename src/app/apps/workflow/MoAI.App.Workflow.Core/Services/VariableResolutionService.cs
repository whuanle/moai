using System.Text.Json;
using System.Text.RegularExpressions;
using Maomi;
using MoAI.Infra.Exceptions;
using MoAI.Workflow.Models;

namespace MoAI.Workflow.Services;

/// <summary>
/// 变量解析服务，负责从节点管道中解析变量引用.
/// 支持系统变量（sys.*）、输入参数（input.*）、节点输出（nodeKey.*）和数组访问（[0]、[*]）.
/// </summary>
[InjectOnScoped]
public class VariableResolutionService
{
    /// <summary>
    /// 从节点管道中解析变量引用.
    /// </summary>
    /// <param name="variableReference">变量引用字符串（如 sys.userId、input.name、nodeA.output[0]）.</param>
    /// <param name="pipeline">节点管道，包含参数解析所需的变量映射.</param>
    /// <returns>解析后的变量值.</returns>
    /// <exception cref="BusinessException">当变量引用无效或不存在时抛出.</exception>
    public object ResolveVariable(string variableReference, INodePipeline pipeline)
    {
        if (string.IsNullOrWhiteSpace(variableReference))
        {
            throw new BusinessException("变量引用不能为空") { StatusCode = 400 };
        }

        // 处理数组访问语法 [0] 或 [*]
        var arrayMatch = Regex.Match(variableReference, @"^(.+?)\[(\d+|\*)\](.*)$");
        if (arrayMatch.Success)
        {
            var basePath = arrayMatch.Groups[1].Value;
            var indexOrWildcard = arrayMatch.Groups[2].Value;
            var remainingPath = arrayMatch.Groups[3].Value;

            // 先解析基础路径
            var baseValue = ResolveVariableInternal(basePath, pipeline);

            // 处理数组访问
            if (baseValue is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
            {
                if (indexOrWildcard == "*")
                {
                    // 通配符访问 - 返回所有元素
                    var results = new List<object>();
                    foreach (var item in jsonElement.EnumerateArray())
                    {
                        if (string.IsNullOrEmpty(remainingPath))
                        {
                            results.Add(ConvertJsonElement(item));
                        }
                        else
                        {
                            // 递归处理剩余路径
                            var itemValue = AccessNestedProperty(item, remainingPath.TrimStart('.'));
                            results.Add(itemValue);
                        }
                    }

                    return results;
                }
                else
                {
                    // 索引访问
                    if (!int.TryParse(indexOrWildcard, out var index))
                    {
                        throw new BusinessException($"无效的数组索引: {indexOrWildcard}") { StatusCode = 400 };
                    }

                    var array = jsonElement.EnumerateArray().ToList();
                    if (index < 0 || index >= array.Count)
                    {
                        throw new BusinessException($"数组索引超出范围: {index}，数组长度: {array.Count}") { StatusCode = 400 };
                    }

                    var element = array[index];
                    if (string.IsNullOrEmpty(remainingPath))
                    {
                        return ConvertJsonElement(element);
                    }
                    else
                    {
                        // 递归处理剩余路径
                        return AccessNestedProperty(element, remainingPath.TrimStart('.'));
                    }
                }
            }
            else if (baseValue is IList<object> list)
            {
                if (indexOrWildcard == "*")
                {
                    // 通配符访问 - 返回所有元素
                    if (string.IsNullOrEmpty(remainingPath))
                    {
                        return list;
                    }
                    else
                    {
                        var results = new List<object>();
                        foreach (var item in list)
                        {
                            var itemValue = AccessNestedPropertyFromObject(item, remainingPath.TrimStart('.'));
                            results.Add(itemValue);
                        }

                        return results;
                    }
                }
                else
                {
                    // 索引访问
                    if (!int.TryParse(indexOrWildcard, out var index))
                    {
                        throw new BusinessException($"无效的数组索引: {indexOrWildcard}") { StatusCode = 400 };
                    }

                    if (index < 0 || index >= list.Count)
                    {
                        throw new BusinessException($"数组索引超出范围: {index}，数组长度: {list.Count}") { StatusCode = 400 };
                    }

                    var element = list[index];
                    if (string.IsNullOrEmpty(remainingPath))
                    {
                        return element;
                    }
                    else
                    {
                        return AccessNestedPropertyFromObject(element, remainingPath.TrimStart('.'));
                    }
                }
            }
            else
            {
                throw new BusinessException($"变量 {basePath} 不是数组类型") { StatusCode = 400 };
            }
        }

        // 普通变量引用（无数组访问）
        return ResolveVariableInternal(variableReference, pipeline);
    }

    /// <summary>
    /// 将嵌套 JSON 对象扁平化为点符号格式的字典.
    /// </summary>
    /// <param name="prefix">前缀（如节点键）.</param>
    /// <param name="element">要扁平化的 JSON 元素.</param>
    /// <returns>扁平化后的字典.</returns>
    public Dictionary<string, object> FlattenJson(string prefix, JsonElement element)
    {
        var result = new Dictionary<string, object>();

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
                    var nested = FlattenJson(key, property.Value);
                    foreach (var kvp in nested)
                    {
                        result[kvp.Key] = kvp.Value;
                    }
                }

                break;

            case JsonValueKind.Array:
                var array = element.EnumerateArray().ToList();
                for (int i = 0; i < array.Count; i++)
                {
                    var key = $"{prefix}[{i}]";
                    var nested = FlattenJson(key, array[i]);
                    foreach (var kvp in nested)
                    {
                        result[kvp.Key] = kvp.Value;
                    }
                }

                // 也添加整个数组作为一个值
                result[prefix] = element;
                break;

            default:
                // 基本类型（String、Number、Boolean、Null）
                result[prefix] = ConvertJsonElement(element);
                break;
        }

        return result;
    }

    /// <summary>
    /// 内部方法：解析变量引用（不处理数组访问）.
    /// </summary>
    private object ResolveVariableInternal(string variableReference, INodePipeline pipeline)
    {
        // 首先尝试从扁平化变量映射中直接获取
        if (pipeline.FlattenedVariables.TryGetValue(variableReference, out var value))
        {
            return value;
        }

        // 解析变量引用的各个部分
        var parts = variableReference.Split('.');
        if (parts.Length < 2)
        {
            throw new BusinessException($"无效的变量引用格式: {variableReference}，应为 prefix.field 格式") { StatusCode = 400 };
        }

        var prefix = parts[0];
        var fieldPath = string.Join(".", parts.Skip(1));

        switch (prefix)
        {
            case "sys":
                // 系统变量
                return ResolveSystemVariable(fieldPath, pipeline);

            case "input":
                // 输入参数
                return ResolveInputParameter(fieldPath, pipeline);

            default:
                // 节点输出（nodeKey.*）
                return ResolveNodeOutput(prefix, fieldPath, pipeline);
        }
    }

    /// <summary>
    /// 解析系统变量.
    /// </summary>
    private object ResolveSystemVariable(string fieldPath, INodePipeline pipeline)
    {
        var key = $"sys.{fieldPath}";
        if (pipeline.FlattenedVariables.TryGetValue(key, out var value))
        {
            return value;
        }

        // 尝试从 SystemVariables 中获取
        if (pipeline.SystemVariables.TryGetValue(fieldPath, out var sysValue))
        {
            return sysValue;
        }

        throw new BusinessException($"系统变量不存在: {key}") { StatusCode = 404 };
    }

    /// <summary>
    /// 解析输入参数.
    /// </summary>
    private object ResolveInputParameter(string fieldPath, INodePipeline pipeline)
    {
        var key = $"input.{fieldPath}";
        if (pipeline.FlattenedVariables.TryGetValue(key, out var value))
        {
            return value;
        }

        // 尝试从 RuntimeParameters 中获取
        if (pipeline.RuntimeParameters.TryGetValue(fieldPath, out var paramValue))
        {
            return paramValue;
        }

        throw new BusinessException($"输入参数不存在: {key}") { StatusCode = 404 };
    }

    /// <summary>
    /// 解析节点输出.
    /// </summary>
    private object ResolveNodeOutput(string nodeKey, string fieldPath, INodePipeline pipeline)
    {
        // 尝试从扁平化变量映射中获取
        var key = $"{nodeKey}.{fieldPath}";
        if (pipeline.FlattenedVariables.TryGetValue(key, out var value))
        {
            return value;
        }

        // 尝试从节点输出映射中获取
        if (pipeline.NodeOutputs.TryGetValue(nodeKey, out var nodeOutput))
        {
            if (nodeOutput.TryGetValue(fieldPath, out var outputValue))
            {
                return outputValue;
            }

            // 尝试访问嵌套属性
            try
            {
                var jsonElement = JsonSerializer.SerializeToElement(nodeOutput);
                return AccessNestedProperty(jsonElement, fieldPath);
            }
            catch
            {
                // 忽略，继续抛出未找到异常
            }
        }

        throw new BusinessException($"节点输出字段不存在: {key}，节点 {nodeKey} 可能尚未执行") { StatusCode = 404 };
    }

    /// <summary>
    /// 从 JsonElement 中访问嵌套属性.
    /// </summary>
    private object AccessNestedProperty(JsonElement element, string path)
    {
        var parts = path.Split('.');
        var current = element;

        foreach (var part in parts)
        {
            if (current.ValueKind != JsonValueKind.Object)
            {
                throw new BusinessException($"无法访问属性 {part}，当前值不是对象类型") { StatusCode = 400 };
            }

            if (!current.TryGetProperty(part, out var property))
            {
                throw new BusinessException($"属性 {part} 不存在") { StatusCode = 404 };
            }

            current = property;
        }

        return ConvertJsonElement(current);
    }

    /// <summary>
    /// 从普通对象中访问嵌套属性.
    /// </summary>
    private object AccessNestedPropertyFromObject(object obj, string path)
    {
        if (obj is JsonElement jsonElement)
        {
            return AccessNestedProperty(jsonElement, path);
        }

        var parts = path.Split('.');
        var current = obj;

        foreach (var part in parts)
        {
            if (current == null)
            {
                throw new BusinessException($"无法访问属性 {part}，当前值为 null") { StatusCode = 400 };
            }

            var type = current.GetType();
            var property = type.GetProperty(part);
            if (property == null)
            {
                throw new BusinessException($"属性 {part} 不存在") { StatusCode = 404 };
            }

            current = property.GetValue(current);
        }

        return current ?? throw new BusinessException($"属性值为 null") { StatusCode = 404 };
    }

    /// <summary>
    /// 将 JsonElement 转换为对应的 .NET 类型.
    /// </summary>
    private object ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.TryGetInt32(out var intValue) ? intValue : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
            JsonValueKind.Object => element,
            _ => element,
        };
    }
}
