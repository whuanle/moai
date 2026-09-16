using System.Text.Json.Nodes;
using Jint;
using MoAI.App.Workflow.Definition;

namespace MoAI.App.Workflow.Nodes.Builtin;

/// <summary>
/// JavaScript 节点执行器 - 使用 Jint 执行 JS 脚本做数据加工（与旧版 WorkflowJavaScriptAction 思路一致）.
/// config: { "code": "function run(inputs, sys, nodes) { return { ... }; }" }
/// 入参：inputs（当前节点输入）、sys（系统变量）、nodes（上游节点输出）；返回值即节点输出.
/// </summary>
public class JavaScriptNodeExecutor : INodeExecutor
{
    private const string WrapperScript = """
        function __invoke__(inputsJson, sysJson, nodesJson) {
          var inputs = JSON.parse(inputsJson);
          var sys = JSON.parse(sysJson);
          var nodes = JSON.parse(nodesJson);
          var result = run(inputs, sys, nodes);
          if (result === undefined) {
            return null;
          }
          return JSON.stringify(result);
        }
        """;

    /// <inheritdoc/>
    public string NodeType => NodeTypes.JavaScript;

    /// <inheritdoc/>
    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var code = context.GetConfigString("code");
        if (string.IsNullOrWhiteSpace(code))
        {
            return Task.FromResult(NodeExecutionResult.Failure("JavaScript 节点缺少配置：config.code"));
        }

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
            engine.Execute(code);

            var result = engine.Invoke(
                "__invoke__",
                context.Inputs.ToJsonString(),
                context.Scope.SystemVariables.ToJsonString(),
                context.Scope.ToJsonPathContext()["nodes"]!.ToJsonString());

            if (result.IsString())
            {
                var output = JsonNode.Parse(result.AsString());
                if (output is JsonObject outputObject)
                {
                    return Task.FromResult(NodeExecutionResult.Success(outputObject));
                }

                return Task.FromResult(NodeExecutionResult.Failure("JavaScript 节点返回值必须是 JSON 对象"));
            }

            // 脚本未返回值时输出为空对象
            return Task.FromResult(NodeExecutionResult.Success(new JsonObject()));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeExecutionResult.Failure($"JavaScript 执行失败：{ex.Message}"));
        }
    }
}
