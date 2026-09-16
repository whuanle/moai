using System.Text.Json.Nodes;
using MoAI.App.Workflow.Definition;

namespace MoAI.App.Workflow.Nodes.Builtin;

/// <summary>
/// 条件节点执行器 - 评估布尔条件，输出 {result: true/false}；
/// 调度器根据 result 选择 condition=true/false 的出边（流程数据路由）.
/// </summary>
public class ConditionNodeExecutor : INodeExecutor
{
    /// <inheritdoc/>
    public string NodeType => NodeTypes.Condition;

    /// <inheritdoc/>
    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        if (!context.Inputs.TryGetPropertyValue("condition", out var conditionValue) || conditionValue == null)
        {
            return Task.FromResult(NodeExecutionResult.Failure("缺少必需的输入字段：condition"));
        }

        bool result;
        if (conditionValue is JsonValue value)
        {
            if (value.TryGetValue<bool>(out var boolValue))
            {
                result = boolValue;
            }
            else if (value.TryGetValue<string>(out var stringValue) && bool.TryParse(stringValue, out var parsed))
            {
                result = parsed;
            }
            else
            {
                return Task.FromResult(NodeExecutionResult.Failure($"条件值无法转换为布尔：{conditionValue.ToJsonString()}"));
            }
        }
        else
        {
            return Task.FromResult(NodeExecutionResult.Failure($"条件值无法转换为布尔：{conditionValue.ToJsonString()}"));
        }

        return Task.FromResult(NodeExecutionResult.Success(new JsonObject
        {
            ["result"] = result,
        }));
    }
}
