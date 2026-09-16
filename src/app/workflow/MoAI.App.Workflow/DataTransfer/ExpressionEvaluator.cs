using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Json.Path;
using MoAI.App.Workflow.Definition;

namespace MoAI.App.Workflow.DataTransfer;

/// <summary>
/// 表达式求值器 - 流程数据传输的核心，把字段绑定表达式解析为具体值.
/// 支持 Fixed（固定值）、Variable（变量引用）、JsonPath（JSON 路径）、Interpolation（字符串插值）.
/// </summary>
public interface IExpressionEvaluator
{
    /// <summary>
    /// 求值字段绑定表达式.
    /// </summary>
    /// <param name="binding">字段绑定.</param>
    /// <param name="scope">变量作用域.</param>
    /// <returns>求值结果.</returns>
    JsonNode? Evaluate(FieldBinding binding, IWorkflowVariableScope scope);
}

/// <summary>
/// <see cref="IExpressionEvaluator"/> 默认实现.
/// </summary>
public partial class ExpressionEvaluator : IExpressionEvaluator
{
    [GeneratedRegex(@"\{([^{}]+)\}")]
    private static partial Regex InterpolationRegex();

    /// <inheritdoc/>
    public JsonNode? Evaluate(FieldBinding binding, IWorkflowVariableScope scope)
    {
        if (binding == null)
        {
            return null;
        }

        return binding.ExpressionType switch
        {
            ExpressionType.Run => throw new WorkflowException("run 类型表达式只能用于开始节点的输出定义，不能作为节点输入"),
            ExpressionType.Fixed => EvaluateFixed(binding.Value),
            ExpressionType.Variable => EvaluateVariable(binding.Value, scope),
            ExpressionType.JsonPath => EvaluateJsonPath(binding.Value, scope),
            ExpressionType.Interpolation => EvaluateInterpolation(binding.Value, scope),
            _ => throw new WorkflowException($"不支持的表达式类型：{binding.ExpressionType}"),
        };
    }

    /// <summary>
    /// 固定值：优先按 JSON 字面量解析（数字/布尔/对象/数组），失败时作为普通字符串.
    /// </summary>
    private static JsonNode? EvaluateFixed(string value)
    {
        if (value == null)
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return JsonValue.Create(string.Empty);
        }

        try
        {
            return JsonNode.Parse(trimmed);
        }
        catch (JsonException)
        {
            return JsonValue.Create(value);
        }
    }

    /// <summary>
    /// 变量引用：从变量作用域解析.
    /// </summary>
    private static JsonNode? EvaluateVariable(string reference, IWorkflowVariableScope scope)
    {
        if (!scope.TryResolve(reference, out var value))
        {
            throw new WorkflowException($"变量引用解析失败：{reference}（变量不存在或节点尚未执行）");
        }

        return value;
    }

    /// <summary>
    /// JSON 路径：在 {sys, input, nodes} 上下文上执行 JsonPath 查询.
    /// </summary>
    private static JsonNode? EvaluateJsonPath(string expression, IWorkflowVariableScope scope)
    {
        JsonPath path;
        try
        {
            path = JsonPath.Parse(expression);
        }
        catch (PathParseException ex)
        {
            throw new WorkflowException($"JsonPath 表达式无效：{expression}，{ex.Message}");
        }

        var context = scope.ToJsonPathContext();
        var results = path.Evaluate(context);

        if (results.Matches.Count == 0)
        {
            throw new WorkflowException($"JsonPath 表达式未匹配到任何结果：{expression}");
        }

        if (results.Matches.Count == 1)
        {
            return results.Matches[0].Value?.DeepClone();
        }

        var array = new JsonArray();
        foreach (var match in results.Matches)
        {
            array.Add(match.Value?.DeepClone());
        }

        return array;
    }

    /// <summary>
    /// 字符串插值：替换模板中的 {变量引用}，如 "请总结：{search.summary}".
    /// 字符串值直接取原文（不带 JSON 引号），对象/数组等复杂值序列化为 JSON 文本.
    /// </summary>
    private JsonNode? EvaluateInterpolation(string template, IWorkflowVariableScope scope)
    {
        var result = InterpolationRegex().Replace(template, match =>
        {
            var reference = match.Groups[1].Value.Trim();
            if (!scope.TryResolve(reference, out var value) || value == null)
            {
                throw new WorkflowException($"插值模板中的变量解析失败：{reference}");
            }

            return value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var stringValue)
                ? stringValue
                : value.ToJsonString();
        });

        return JsonValue.Create(result);
    }
}
