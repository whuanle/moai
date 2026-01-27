using Jint;
using MoAI.Infra.Exceptions;
using MoAI.Workflow.Enums;
using MoAI.Workflow.Models;
using System.Text.Json;

namespace MoAI.Workflow.Runtime;

/// <summary>
/// JavaScript 节点运行时实现.
/// JavaScript 节点负责执行 JavaScript 代码并提供对工作流上下文的访问.
/// </summary>
public class JavaScriptNodeRuntime : INodeRuntime
{
    /// <inheritdoc/>
    public NodeType SupportedNodeType => NodeType.JavaScript;

    /// <summary>
    /// 执行 JavaScript 节点逻辑.
    /// 执行提供的 JavaScript 代码，并将工作流上下文作为全局变量提供给脚本.
    /// </summary>
    /// <param name="nodeDefine">节点定义.</param>
    /// <param name="inputs">节点输入数据，应包含 code 字段.</param>
    /// <param name="context">工作流上下文，将作为全局变量提供给 JavaScript 代码.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>包含 JavaScript 执行结果的执行结果.</returns>
    public Task<NodeExecutionResult> ExecuteAsync(
        INodeDefine nodeDefine,
        Dictionary<string, object> inputs,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1. 验证必需的输入字段
            if (!inputs.TryGetValue("code", out var codeObj))
            {
                return Task.FromResult(NodeExecutionResult.Failure("缺少必需的输入字段: code"));
            }

            string code = codeObj?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(code))
            {
                return Task.FromResult(NodeExecutionResult.Failure("JavaScript 代码不能为空"));
            }

            // 2. 准备工作流上下文数据
            var contextData = PrepareContextData(context);

            // 3. 执行 JavaScript 代码
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

            // 4. 构建输出
            var output = new Dictionary<string, object>();

            if (result != null)
            {
                // 如果结果是字典类型，展开到输出中
                if (result is Dictionary<string, object> resultDict)
                {
                    foreach (var kvp in resultDict)
                    {
                        output[kvp.Key] = kvp.Value;
                    }
                }
                else
                {
                    // 否则将结果作为 result 字段
                    output["result"] = result;
                }
            }
            else
            {
                // 如果没有返回值，提供空结果
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
    /// 准备工作流上下文数据，将其转换为 JavaScript 可访问的格式.
    /// </summary>
    /// <param name="context">工作流上下文.</param>
    /// <returns>包含上下文数据的字典.</returns>
    private Dictionary<string, object> PrepareContextData(IWorkflowContext context)
    {
        var contextData = new Dictionary<string, object>
        {
            ["instanceId"] = context.InstanceId,
            ["definitionId"] = context.DefinitionId,
            ["input"] = context.RuntimeParameters,
            ["variables"] = context.FlattenedVariables,
            ["executedNodes"] = context.ExecutedNodeKeys.ToList()
        };

        // 添加系统变量
        var sysVariables = new Dictionary<string, object>();
        foreach (var kvp in context.FlattenedVariables)
        {
            if (kvp.Key.StartsWith("sys.", StringComparison.OrdinalIgnoreCase))
            {
                var key = kvp.Key.Substring(4); // 移除 "sys." 前缀
                sysVariables[key] = kvp.Value;
            }
        }
        contextData["sys"] = sysVariables;

        // 添加节点输出
        var nodeOutputs = new Dictionary<string, object>();
        foreach (var kvp in context.NodePipelines)
        {
            if (kvp.Value.OutputJsonMap != null && kvp.Value.OutputJsonMap.Count > 0)
            {
                nodeOutputs[kvp.Key] = kvp.Value.OutputJsonMap;
            }
        }
        contextData["nodes"] = nodeOutputs;

        return contextData;
    }

    /// <summary>
    /// 执行 JavaScript 代码并返回结果.
    /// </summary>
    /// <param name="code">JavaScript 代码.</param>
    /// <param name="contextData">工作流上下文数据.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>JavaScript 执行结果.</returns>
    private object? ExecuteJavaScript(
        string code,
        Dictionary<string, object> contextData,
        CancellationToken cancellationToken)
    {
        try
        {
            using var engine = new Engine(options =>
            {
                // 限制内存为 4 MB
                options.LimitMemory(4_000_000);

                // 限制递归深度为 100
                options.LimitRecursion(100);

                // 超时时间为 4 秒
                options.TimeoutInterval(TimeSpan.FromSeconds(4));

                // 最大执行语句数为 1000
                options.MaxStatements(1000);

                // 使用取消令牌
                options.CancellationToken(cancellationToken);
            });

            // 将上下文数据注入到 JavaScript 引擎中
            engine.SetValue("context", contextData);
            engine.SetValue("input", contextData["input"]);
            engine.SetValue("sys", contextData["sys"]);
            engine.SetValue("nodes", contextData["nodes"]);
            engine.SetValue("variables", contextData["variables"]);

            // 添加 JSON 辅助函数
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

            // 执行用户代码
            var completionValue = engine.Evaluate(code);

            // 转换结果
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
