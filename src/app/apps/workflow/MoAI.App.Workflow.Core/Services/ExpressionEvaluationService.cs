using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Path;
using MoAI.Infra.Exceptions;
using MoAI.Workflow.Enums;
using MoAI.Workflow.Models;
using SmartFormat;

namespace MoAI.Workflow.Services;

/// <summary>
/// 表达式评估服务，负责评估不同类型的字段表达式.
/// 支持 Fixed（固定值）、Variable（变量引用）、JsonPath（JSON 路径）和 StringInterpolation（字符串插值）.
/// </summary>
public class ExpressionEvaluationService
{
    private readonly VariableResolutionService _variableResolutionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionEvaluationService"/> class.
    /// </summary>
    /// <param name="variableResolutionService">变量解析服务.</param>
    public ExpressionEvaluationService(VariableResolutionService variableResolutionService)
    {
        _variableResolutionService = variableResolutionService;
    }

    /// <summary>
    /// 评估字段表达式并返回结果值.
    /// </summary>
    /// <param name="expression">表达式字符串.</param>
    /// <param name="expressionType">表达式类型.</param>
    /// <param name="context">工作流上下文.</param>
    /// <returns>评估后的值.</returns>
    /// <exception cref="BusinessException">当表达式评估失败时抛出.</exception>
    public object EvaluateExpression(
        string expression,
        FieldExpressionType expressionType,
        IWorkflowContext context)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return string.Empty;
        }

        return expressionType switch
        {
            FieldExpressionType.Fixed => EvaluateFixed(expression),
            FieldExpressionType.Variable => EvaluateVariable(expression, context),
            FieldExpressionType.Jsonpath => EvaluateJsonPath(expression, context),
            FieldExpressionType.Interpolation => EvaluateStringInterpolation(expression, context),
            _ => throw new BusinessException($"不支持的表达式类型: {expressionType}") { StatusCode = 400 },
        };
    }

    /// <summary>
    /// 评估 Fixed 表达式 - 直接返回固定值.
    /// </summary>
    /// <param name="expression">固定值字符串.</param>
    /// <returns>固定值.</returns>
    private object EvaluateFixed(string expression)
    {
        return expression;
    }

    /// <summary>
    /// 评估 Variable 表达式 - 从工作流上下文中解析变量引用.
    /// </summary>
    /// <param name="expression">变量引用（如 sys.userId、input.name、nodeKey.output）.</param>
    /// <param name="context">工作流上下文.</param>
    /// <returns>变量值.</returns>
    private object EvaluateVariable(string expression, IWorkflowContext context)
    {
        try
        {
            return _variableResolutionService.ResolveVariable(expression, context);
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new BusinessException($"变量解析失败: {expression}, 错误: {ex.Message}") { StatusCode = 400 };
        }
    }

    /// <summary>
    /// 评估 JsonPath 表达式 - 使用 JsonPath.Net 解析 JSON 路径.
    /// </summary>
    /// <param name="expression">JsonPath 表达式（如 $.nodeA.result[0].name）.</param>
    /// <param name="context">工作流上下文.</param>
    /// <returns>JsonPath 查询结果.</returns>
    private object EvaluateJsonPath(string expression, IWorkflowContext context)
    {
        try
        {
            // 解析 JsonPath 表达式
            var jsonPath = Json.Path.JsonPath.Parse(expression);

            // 将工作流上下文转换为 JsonElement 然后转为 JsonNode
            var contextJson = JsonSerializer.SerializeToElement(new
            {
                sys = GetSystemVariables(context),
                input = context.RuntimeParameters,
                nodes = GetNodeOutputs(context),
            });

            // 转换为 JsonNode
            var contextNode = JsonNode.Parse(contextJson.GetRawText());
            if (contextNode == null)
            {
                throw new BusinessException($"无法解析工作流上下文为 JSON: {expression}") { StatusCode = 400 };
            }

            // 执行 JsonPath 查询
            var result = jsonPath.Evaluate(contextNode);

            if (result.Matches == null || result.Matches.Count == 0)
            {
                throw new BusinessException($"JsonPath 表达式未匹配到任何结果: {expression}") { StatusCode = 404 };
            }

            // 如果只有一个匹配结果，返回该值
            if (result.Matches.Count == 1)
            {
                return ConvertJsonNode(result.Matches[0].Value);
            }

            // 如果有多个匹配结果，返回数组
            return result.Matches.Select(m => ConvertJsonNode(m.Value)).ToList();
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new BusinessException($"JsonPath 表达式评估失败: {expression}, 错误: {ex.Message}") { StatusCode = 400 };
        }
    }

    /// <summary>
    /// 评估 StringInterpolation 表达式 - 使用 SmartFormat.NET 进行字符串插值.
    /// </summary>
    /// <param name="expression">字符串插值模板（如 "Hello {input.name}, your ID is {sys.userId}"）.</param>
    /// <param name="context">工作流上下文.</param>
    /// <returns>插值后的字符串.</returns>
    private object EvaluateStringInterpolation(string expression, IWorkflowContext context)
    {
        try
        {
            // 构建数据对象供 SmartFormat 使用
            var data = new Dictionary<string, object>
            {
                ["sys"] = GetSystemVariables(context),
                ["input"] = context.RuntimeParameters,
            };

            // 添加节点输出
            foreach (var nodeKey in context.ExecutedNodeKeys)
            {
                if (context.NodePipelines.TryGetValue(nodeKey, out var pipeline))
                {
                    data[nodeKey] = pipeline.OutputJsonMap;
                }
            }

            // 使用 SmartFormat 进行字符串插值
            var result = Smart.Format(expression, data);
            return result;
        }
        catch (Exception ex)
        {
            throw new BusinessException($"字符串插值失败: {expression}, 错误: {ex.Message}") { StatusCode = 400 };
        }
    }

    /// <summary>
    /// 从工作流上下文中提取系统变量.
    /// </summary>
    /// <param name="context">工作流上下文.</param>
    /// <returns>系统变量字典.</returns>
    private Dictionary<string, object> GetSystemVariables(IWorkflowContext context)
    {
        var sysVars = new Dictionary<string, object>();

        foreach (var kvp in context.FlattenedVariables)
        {
            if (kvp.Key.StartsWith("sys."))
            {
                var key = kvp.Key.Substring(4); // 移除 "sys." 前缀
                sysVars[key] = kvp.Value;
            }
        }

        return sysVars;
    }

    /// <summary>
    /// 从工作流上下文中提取所有节点输出.
    /// </summary>
    /// <param name="context">工作流上下文.</param>
    /// <returns>节点输出字典.</returns>
    private Dictionary<string, object> GetNodeOutputs(IWorkflowContext context)
    {
        var nodeOutputs = new Dictionary<string, object>();

        foreach (var nodeKey in context.ExecutedNodeKeys)
        {
            if (context.NodePipelines.TryGetValue(nodeKey, out var pipeline))
            {
                nodeOutputs[nodeKey] = pipeline.OutputJsonMap;
            }
        }

        return nodeOutputs;
    }

    /// <summary>
    /// 将 JsonNode 转换为对应的 .NET 类型.
    /// </summary>
    /// <param name="node">JSON 节点.</param>
    /// <returns>转换后的值.</returns>
    private object? ConvertJsonNode(JsonNode? node)
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
            JsonArray array => array.Select(ConvertJsonNode).ToList(),
            JsonObject obj => obj.ToDictionary(
                kvp => kvp.Key,
                kvp => ConvertJsonNode(kvp.Value)),
            JsonValue value when value.GetValueKind() == JsonValueKind.Null => null,
            _ => node.ToString()
        };
    }
}
