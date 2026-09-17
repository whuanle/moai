using System.Text.Json;
using System.Text.Json.Nodes;
using MoAI.App.Workflow.DataTransfer;
using MoAI.App.Workflow.Definition;

namespace MoAI.App.Workflow.Nodes.Builtin;

/// <summary>
/// 多条件节点执行器（if-else）- 按顺序评估 config.branches 中的条件绑定，
/// 第一个为真的分支命中：输出 = 输入透传 + {result: 分支 id}；全部未命中时 result = "else"。
/// 调度器按 result 匹配出边的 condition 标记（分支 id 或 else）.
/// config: { "branches": [ { "id": "b1", "label": "条件1", "binding": { "expressionType": "variable", "value": "start.query" } } ] }
/// </summary>
public class SwitchNodeExecutor : INodeExecutor
{
    private readonly IExpressionEvaluator _expressionEvaluator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SwitchNodeExecutor"/> class.
    /// </summary>
    public SwitchNodeExecutor(IExpressionEvaluator expressionEvaluator)
    {
        _expressionEvaluator = expressionEvaluator;
    }

    /// <inheritdoc/>
    public string NodeType => NodeTypes.Switch;

    /// <inheritdoc/>
    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var branches = ParseBranches(context);
        if (branches.Count == 0)
        {
            return Task.FromResult(NodeExecutionResult.Failure("多条件节点缺少配置：config.branches（至少需要一个分支）"));
        }

        var matched = "else";
        foreach (var branch in branches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = _expressionEvaluator.Evaluate(branch.Binding, context.Scope);
            if (IsTruthy(value))
            {
                matched = branch.Id;
                break;
            }
        }

        // 输入透传 + 命中分支（供下游引用走哪个分支）
        var output = context.Inputs.CloneObject();
        output["result"] = matched;
        return Task.FromResult(NodeExecutionResult.Success(output));
    }

    private static List<(string Id, FieldBinding Binding)> ParseBranches(NodeExecutionContext context)
    {
        var list = new List<(string, FieldBinding)>();
        if (context.Config.ValueKind != JsonValueKind.Object || !context.Config.TryGetProperty("branches", out var branchesEl) || branchesEl.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var item in branchesEl.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(id) || !item.TryGetProperty("binding", out var bindingEl))
            {
                continue;
            }

            FieldBinding? binding;
            try
            {
                binding = bindingEl.Deserialize<FieldBinding>(WorkflowJson.Options);
            }
            catch (JsonException)
            {
                continue;
            }

            if (binding != null)
            {
                list.Add((id, binding));
            }
        }

        return list;
    }

    /// <summary>
    /// 真值判断：布尔取自身；字符串 "true" 为真（"false"/空串为假）；数字非零为真；对象/数组非空为真.
    /// </summary>
    private static bool IsTruthy(JsonNode? value)
    {
        if (value == null)
        {
            return false;
        }

        if (value is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<bool>(out var boolValue))
            {
                return boolValue;
            }

            if (jsonValue.TryGetValue<string>(out var stringValue))
            {
                return !string.IsNullOrEmpty(stringValue) && !string.Equals(stringValue, "false", StringComparison.OrdinalIgnoreCase);
            }

            if (jsonValue.TryGetValue<long>(out var numberValue))
            {
                return numberValue != 0;
            }

            if (jsonValue.TryGetValue<double>(out var doubleValue))
            {
                return doubleValue != 0;
            }
        }

        return true;
    }
}
