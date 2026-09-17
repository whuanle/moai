using System.Text.Json.Nodes;
using Jint;
using MoAI.App.Workflow.Definition;

namespace MoAI.App.Workflow.Nodes.Builtin;

/// <summary>
/// 条件节点执行器 - 评估布尔条件并透传输入：输出 = 输入 + {result: true/false}；
/// 调度器根据 result 选择 condition=true/false 的出边（流程数据路由），
/// 下游可引用 result 或节点输入中的任意透传字段.
/// 两种求值方式：①绑定模式——按 condition 输入绑定求值（变量/JsonPath/插值/固定值）；
/// ②脚本模式——配置 config.conditionScript（约定 function condition(inputs, sys, nodes, system) 返回布尔），
/// 存在时优先于绑定.
/// </summary>
public class ConditionNodeExecutor : INodeExecutor
{
    private const string WrapperScript = """
        function __condition__(inputsJson, sysJson, nodesJson, systemJson) {
          var inputs = JSON.parse(inputsJson);
          var sys = JSON.parse(sysJson);
          var nodes = JSON.parse(nodesJson);
          var system = JSON.parse(systemJson);
          var result = condition(inputs, sys, nodes, system);
          if (result === undefined) {
            return null;
          }
          return JSON.stringify(result);
        }
        """;

    /// <inheritdoc/>
    public string NodeType => NodeTypes.Condition;

    /// <inheritdoc/>
    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var script = context.GetConfigString("conditionScript");
        if (!string.IsNullOrWhiteSpace(script))
        {
            return ExecuteScriptAsync(context, script, cancellationToken);
        }

        return ExecuteBindingAsync(context);
    }

    /// <summary>
    /// 脚本模式：Jint 执行 conditionScript，返回值必须可转布尔.
    /// </summary>
    private Task<NodeExecutionResult> ExecuteScriptAsync(NodeExecutionContext context, string script, CancellationToken cancellationToken)
    {
        bool result;
        try
        {
            using var engine = new Engine(options =>
            {
                options.LimitMemory(4_000_000);
                options.TimeoutInterval(TimeSpan.FromSeconds(10));
                options.MaxStatements(100_000);
                options.CancellationToken(cancellationToken);
            });

            engine.Execute(WrapperScript);
            engine.Execute(script);

            var scriptResult = engine.Invoke(
                "__condition__",
                context.Inputs.ToJsonString(),
                context.Scope.SystemVariables.ToJsonString(),
                context.Scope.ToJsonPathContext()["nodes"]!.ToJsonString(),
                context.Scope.WorkflowGlobals.ToJsonString());

            if (scriptResult.IsBoolean())
            {
                result = scriptResult.AsBoolean();
            }
            else if (scriptResult.IsString() && bool.TryParse(scriptResult.AsString(), out var parsed))
            {
                result = parsed;
            }
            else
            {
                return Task.FromResult(NodeExecutionResult.Failure("条件脚本必须返回布尔值（true/false）"));
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeExecutionResult.Failure($"条件脚本执行失败：{ex.Message}"));
        }

        return Task.FromResult(NodeExecutionResult.Success(BuildOutput(context.Inputs, result)));
    }

    /// <summary>
    /// 绑定模式：按 condition 输入绑定求值（变量/JsonPath/插值/固定值）.
    /// </summary>
    private Task<NodeExecutionResult> ExecuteBindingAsync(NodeExecutionContext context)
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

        return Task.FromResult(NodeExecutionResult.Success(BuildOutput(context.Inputs, result)));
    }

    /// <summary>
    /// 透传：输入原样进入输出（输入什么就输出什么），再附上条件结果.
    /// </summary>
    private static JsonObject BuildOutput(JsonObject inputs, bool result)
    {
        var output = inputs.CloneObject();
        output["result"] = result;
        return output;
    }
}
