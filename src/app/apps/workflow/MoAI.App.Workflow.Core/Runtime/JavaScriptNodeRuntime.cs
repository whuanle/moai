using Jint;
using Maomi;
using MoAI.Infra.Exceptions;
using MoAI.Workflow.Enums;
using MoAI.Workflow.Models;

namespace MoAI.Workflow.Runtime;

/// <summary>
/// JavaScript 节点运行时实现.
/// JavaScript 节点负责执行 JavaScript 代码并提供对节点管道上下文的访问.
/// </summary>
[InjectOnTransient(ServiceKey = NodeType.JavaScript)]
public class JavaScriptNodeRuntime : INodeRuntime
{
    /// <inheritdoc/>
    public NodeType SupportedNodeType => NodeType.JavaScript;

    /// <summary>
    /// 执行 JavaScript 节点逻辑.
    /// </summary>
    public Task<NodeExecutionResult> ExecuteAsync(
        Dictionary<string, object> inputs,
        INodePipeline pipeline,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!inputs.TryGetValue("code", out var codeObj))
            {
                return Task.FromResult(NodeExecutionResult.Failure("缺少必需的输入字段: code"));
            }

            string code = codeObj?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(code))
            {
                return Task.FromResult(NodeExecutionResult.Failure("JavaScript 代码不能为空"));
            }

            var contextData = PrepareContextData(pipeline);

            object? result;
            try
            {
                result = ExecuteJavaScript(code, contextData, cancellationToken);
            }
            catch (BusinessException bex)
            {
                return Task.FromResult(NodeExecutionResult.Failure($"JavaScript 执行失败: {bex.Message}"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(NodeExecutionResult.Failure($"JavaScript 执行异常: {ex.Message}"));
            }

            var output = new Dictionary<string, object>();

            if (result != null)
            {
                if (result is Dictionary<string, object> resultDict)
                {
                    foreach (var kvp in resultDict)
                    {
                        output[kvp.Key] = kvp.Value;
                    }
                }
                else
                {
                    output["result"] = result;
                }
            }
            else
            {
                output["result"] = string.Empty;
            }

            return Task.FromResult(NodeExecutionResult.Success(output));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeExecutionResult.Failure(ex));
        }
    }

    /// <summary>
    /// 准备节点管道上下文数据.
    /// </summary>
    private Dictionary<string, object> PrepareContextData(INodePipeline pipeline)
    {
        var contextData = new Dictionary<string, object>
        {
            ["input"] = pipeline.RuntimeParameters,
            ["variables"] = pipeline.FlattenedVariables,
            ["sys"] = pipeline.SystemVariables,
            ["nodes"] = pipeline.NodeOutputs
        };

        return contextData;
    }

    /// <summary>
    /// 执行 JavaScript 代码并返回结果.
    /// </summary>
    private object? ExecuteJavaScript(
        string code,
        Dictionary<string, object> contextData,
        CancellationToken cancellationToken)
    {
        try
        {
            using var engine = new Engine(options =>
            {
                options.LimitMemory(4_000_000);
                options.LimitRecursion(100);
                options.TimeoutInterval(TimeSpan.FromSeconds(4));
                options.MaxStatements(1000);
                options.CancellationToken(cancellationToken);
            });

            engine.SetValue("context", contextData);
            engine.SetValue("input", contextData["input"]);
            engine.SetValue("sys", contextData["sys"]);
            engine.SetValue("nodes", contextData["nodes"]);
            engine.SetValue("variables", contextData["variables"]);

            engine.Execute(@"
                var JSON = {
                    stringify: function(obj) {
                        return JSON.stringify(obj);
                    },
                    parse: function(str) {
                        return JSON.parse(str);
                    }
                };
            ");

            var completionValue = engine.Evaluate(code);

            if (completionValue.IsNull() || completionValue.IsUndefined())
            {
                return null;
            }

            return completionValue.ToObject();
        }
        catch (Jint.Runtime.JavaScriptException jsEx)
        {
            throw new BusinessException($"JavaScript 运行时错误: {jsEx.Message}") { StatusCode = 400 };
        }
        catch (Exception ex)
        {
            throw new BusinessException($"JavaScript 执行失败: {ex.Message}") { StatusCode = 400 };
        }
    }
}
