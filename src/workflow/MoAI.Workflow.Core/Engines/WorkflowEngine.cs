using Jint.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Helpers;
using MoAI.Workflow.Helpers;
using MoAI.Workflow.Models;
using MoAI.Workflow.Models.NodeDefinitions;
using Newtonsoft.Json.Linq;
using System.Buffers;
using System.Text;

namespace MoAI.Workflow.Engines;

/// <summary>
/// 流程执行引擎.
/// </summary>
public class WorkflowEngine
{
    private readonly WorlflowDefinition _worlflowDefinition;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowEngine"/> class.
    /// </summary>
    /// <param name="worlflowDefinition"></param>
    public WorkflowEngine(WorlflowDefinition worlflowDefinition)
    {
        _worlflowDefinition = worlflowDefinition;
    }

    public async Task StartAsync(IServiceProvider serviceProvider, WorkflowRun run, CancellationToken cancellationToken = default)
    {
        byte[] byteArray = Encoding.UTF8.GetBytes(run.InputJson);
        var inputParamters = ReadJsonHelper.Read(new ReadOnlySequence<byte>(byteArray), new System.Text.Json.JsonReaderOptions { MaxDepth = 3 });

        var systemVariables = new Dictionary<string, object>
        {
            { "sys.user_id", run.UserId },
            { "sys.app_id", run.AppId },
            { "sys.workflow_id", run.DefinitionId },
            { "sys.workflow_run_id", run.InstanceId },
        };

        var workflowContext = new DefaultWorkflowContext
        {
            ServiceProvider = serviceProvider,
            CurrntNode = null!,
            InstanceId = run.InstanceId,
            DefinitionId = run.DefinitionId,
            AppId = run.AppId,
            UserId = run.UserId,
            StartTime = DateTimeOffset.UtcNow,
            Status = WorkflowRunStatus.Running,
            GlobalVariables = inputParamters,
            SystemVariables = systemVariables,
            SystemVariablesJson = JsonParseHelper.Write(systemVariables),
            GlobalVariablesJson = JsonParseHelper.Write(inputParamters),
        };

        // 使用队列实现流程编排节点的处理
        Queue<WorkflowNodefinition> workflowNodefinitions = new Queue<WorkflowNodefinition>();
        workflowNodefinitions.Enqueue(_worlflowDefinition.StartNode);

        while (workflowNodefinitions.Count > 0)
        {
            var nodeDefinition = workflowNodefinitions.Dequeue();

            // 当前节点
            DefaultWorkflowNodeRunResult workflowNodeRunResult = new()
            {
                DefinitionId = nodeDefinition.Id,
                InstanceId = Guid.CreateVersion7(),
                InputJson = run.InputJson,
                StartTime = DateTimeOffset.Now,
                Status = WorkflowNodeRunStatus.Running,
                LastNodeInstanceId = workflowContext.CurrntNode?.InstanceId ?? default!,
                LastNodeDefinitionId = workflowContext.CurrntNode?.DefinitionId ?? default!,
            };

            DefaultWorkflowNodeRunResult lastNode = (workflowContext.CurrntNode == null ? null : workflowContext.CurrntNode as DefaultWorkflowNodeRunResult)!;
            WorkflowNodefinition? nextNodeDefinition = default;
            workflowContext.AddRunningNode(workflowNodeRunResult);

            // 开始节点，没有上一个，但是有下一个
            if (nodeDefinition is WorkflowStartNodeDefinition startNodeDefinition)
            {
                await StartNodeAction(startNodeDefinition, workflowContext, workflowNodeRunResult, cancellationToken);
                nextNodeDefinition = _worlflowDefinition.Nodes.FirstOrDefault(n => n.Id == startNodeDefinition.ConnectionNodeId);
            }
            else
            {
                workflowNodeRunResult.LastNodeDefinitionId = lastNode.DefinitionId;
                workflowNodeRunResult.LastNodeInstanceId = lastNode.InstanceId;
                workflowNodeRunResult.InputJson = lastNode.OutputJson; // 上一个节点的输出作为当前节点的输入

                // 没有下一个
                if (nodeDefinition is WorkflowEndNodeDefinition endNodeDefinition)
                {
                    // 结束节点，直接设置状态为成功
                    // workflowNodeRunResult.Status = WorkflowNodeRunStatus.Success;
                    // workflowNodeRunResult.OutputJson = string.Empty;
                    // workflowNodeRunResult.EndTime = DateTimeOffset.Now;
                    workflowContext.Status = WorkflowRunStatus.Completed;

                    break;
                }
                else
                {
                    lastNode!.NextNodeDefinitionId = workflowNodeRunResult.DefinitionId;
                    lastNode!.NextNodeInstanceId = workflowNodeRunResult.InstanceId;

                    if (nodeDefinition is WorkflowJavaScriptExecuteNodefinition javascriptNodeDefinition)
                    {
                        await JavaScriptNodeAction(javascriptNodeDefinition, workflowContext, workflowNodeRunResult, cancellationToken);

                        nextNodeDefinition = _worlflowDefinition.Nodes.FirstOrDefault(n => n.Id == javascriptNodeDefinition.ConnectionNodeId);
                    }
                    else if (nodeDefinition is WorkflowAiQuestionNodeDefinition aiQuestionNodefinition)
                    {
                    }
                }
            }
        }

    private static async Task JavaScriptNodeAction(
        WorkflowJavaScriptExecuteNodefinition javascriptNodeDefinition,
        DefaultWorkflowContext workflowContext,
        DefaultWorkflowNodeRunResult workflowNodeRunResult,
        CancellationToken cancellationToken)
    {
        using var serviceScope = workflowContext.ServiceProvider.CreateScope();
        var serviceProvider = serviceScope.ServiceProvider;
        var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();

        try
        {
            // 1，整理输入参数
            var inputJson = workflowNodeRunResult.InputJson;

            // 解析 json 生成两种形式的参数.
            (JObject nodeJsonObject, IReadOnlyDictionary<string, object> inputParamters) = ParseJsonParamter(workflowContext, inputJson);

            // 2，取得 javascript 字段的值，生成脚本字符串
            var paramters = WorkflowValueHelper.ParseFieldValue(nodeJsonObject, inputParamters, new WorkflowFieldDefinition
            {
                IsRequired = true,
                Key = "javascript",
                Name = "javascript",
                Type = WorkflowFieldType.String,
                Expression = new WorkflowFieldValuationExpression
                {
                    ExpressionType = javascriptNodeDefinition.InputExpressionType,
                    ExpressionValue = javascriptNodeDefinition.InputExpression,
                }
            });

            var javaScript = paramters["javascript"].ToString() ?? throw new BusinessException("不能获取到 javascript 字段的值");

            WorkflowJavaScriptAction workflowJavaScriptAction = new WorkflowJavaScriptAction();
            var outputJson = workflowJavaScriptAction.Inovke(
                javaScript,
                workflowNodeRunResult.InputJson,
                workflowContext.SystemVariablesJson,
                workflowContext.GlobalVariablesJson,
                cancellationToken);

            workflowNodeRunResult.Status = WorkflowNodeRunStatus.Outputing;
            workflowNodeRunResult.OutputJson = outputJson;
            await workflowContext.OnNodeRunResultChangedAsync(workflowNodeRunResult);

            // 3，将输出的 json 生成按照输出表达式重新生成 json
            if (javascriptNodeDefinition.IsDynamic)
            {
                // 动态输出，直接使用输出的 json
                workflowNodeRunResult.OutputJson = outputJson;
            }
            else
            {
                // 固定输出，按照输出表达式重新生成 json
                workflowNodeRunResult.OutputJson = WorkflowValueHelper.ConvertFixedJsonOutput(outputJson, javascriptNodeDefinition.OutputFields, new Dictionary<string, object>());
            }

            workflowNodeRunResult.Status = WorkflowNodeRunStatus.Success;
            await workflowContext.OnNodeRunResultChangedAsync(workflowNodeRunResult);
        }
        catch (JavaScriptException ex)
        {
            workflowNodeRunResult.Status = WorkflowNodeRunStatus.Exception;
            workflowNodeRunResult.ErrorMessage = $"JavaScript 脚本执行异常{ex.Message}.";

            workflowContext.Status = WorkflowRunStatus.Exception;
            logger.LogError(ex, "JavaScript execution error");
        }
        catch (BusinessException ex)
        {
            workflowNodeRunResult.Status = WorkflowNodeRunStatus.Exception;
            workflowNodeRunResult.ErrorMessage = ex.Message;
            workflowNodeRunResult.EndTime = DateTimeOffset.Now;

            workflowContext.Status = WorkflowRunStatus.Exception;
            logger.LogError(ex, "JavaScript execution error");
        }
        catch (Exception ex)
        {
            workflowNodeRunResult.Status = WorkflowNodeRunStatus.Exception;
            workflowNodeRunResult.ErrorMessage = ex.Message;

            workflowContext.Status = WorkflowRunStatus.Exception;
            logger.LogError(ex, "JavaScript execution error");
        }

        workflowNodeRunResult.EndTime = DateTimeOffset.Now;

        await workflowContext.OnNodeRunResultChangedAsync(workflowNodeRunResult);
        await workflowContext.OnWorkflowRunStatusChangedAsync(workflowContext.Status);
    }

    public static string ParseJsonOutputJson(
        string jsonText,
        IReadOnlyCollection<WorkflowFieldDefinition> fieldDefinitions,
        WorkflowOutputExpressionType outputType,
        DefaultWorkflowContext workflowContext,
        string javaScript,
        CancellationToken cancellationToken)
    {
        // 3，将输出的 json 生成按照输出表达式重新生成 json
        if (outputType == WorkflowOutputExpressionType.Dynamic)
        {
            // 动态输出，直接使用输出的 json
            return jsonText;
        }
        else if (outputType == WorkflowOutputExpressionType.JavaScript)
        {
            // JavaScript 输出，使用 JavaScript 执行输出表达式
            WorkflowJavaScriptAction workflowJavaScriptAction = new WorkflowJavaScriptAction();
            var outputJson = workflowJavaScriptAction.Inovke(
                javaScript,
                jsonText,
                workflowContext.SystemVariablesJson,
                workflowContext.GlobalVariablesJson,
                cancellationToken);

            return outputJson;
        }
        else if (outputType == WorkflowOutputExpressionType.Fixed)
        {
            Dictionary<string, object> outputJsonDict = new Dictionary<string, object>();
            foreach (var field in workflowContext.SystemVariables)
            {
                outputJsonDict.Add(field.Key, field.Value);
            }

            foreach (var field in workflowContext.GlobalVariables)
            {
                outputJsonDict.Add(field.Key, field.Value);
            }

            // 固定输出，按照输出表达式重新生成 json
            var outputJson = WorkflowValueHelper.ConvertFixedJsonOutput(jsonText, fieldDefinitions, outputJsonDict);
            return outputJson;
        }

        return "{}";
    }

    // 解析 json 生成两种形式的参数
    private static (JObject JsonObject, IReadOnlyDictionary<string, object> Paramters) ParseJsonParamter(DefaultWorkflowContext workflowContext, string inputJson)
    {
        var nodeJsonObject = JObject.Parse(inputJson);
        byte[] byteArray = Encoding.UTF8.GetBytes(s: inputJson);
        var inputParamters = ReadJsonHelper.Read(new ReadOnlySequence<byte>(byteArray), new System.Text.Json.JsonReaderOptions { MaxDepth = 3 });
        foreach (var item in workflowContext.SystemVariables)
        {
            inputParamters.TryAdd(item.Key, item.Value);
            nodeJsonObject.Add(item.Key, JToken.FromObject(item.Value));
        }

        foreach (var item in workflowContext.GlobalVariables)
        {
            inputParamters.TryAdd(item.Key, item.Value);
            nodeJsonObject.Add(item.Key, JToken.FromObject(item.Value));
        }

        return (nodeJsonObject, inputParamters);
    }

    private static async Task StartNodeAction(
        WorkflowStartNodeDefinition startNodeDefinition,
        DefaultWorkflowContext workflowContext,
        DefaultWorkflowNodeRunResult workflowNodeRunResult,
        CancellationToken cancellationToken)
    {
        using var serviceScope = workflowContext.ServiceProvider.CreateScope();
        var serviceProvider = serviceScope.ServiceProvider;
        var logger = serviceProvider.GetRequiredService<ILogger<WorkflowEngine>>();

        try
        {
            // 要返回节点执行结果
            WorkflowStartNodeAction workflowStartNodeAction = new WorkflowStartNodeAction(startNodeDefinition);
            var outputJson = workflowStartNodeAction.Inovke(workflowNodeRunResult.InputJson, workflowContext, cancellationToken);
            workflowNodeRunResult.OutputJson = outputJson;
            workflowNodeRunResult.Status = WorkflowNodeRunStatus.Outputing;

            await workflowContext.OnNodeRunResultChangedAsync(nodeRunResult: workflowNodeRunResult);

            // 整理输出参数
            workflowNodeRunResult.OutputJson = outputJson;

            outputJson = ParseJsonOutputJson(outputJson, startNodeDefinition.OutputFields, startNodeDefinition.OutputType, workflowContext, startNodeDefinition.JavaScript, cancellationToken);

            workflowNodeRunResult.Status = WorkflowNodeRunStatus.Success;
            workflowNodeRunResult.EndTime = DateTimeOffset.Now;
            await workflowContext.OnNodeRunResultChangedAsync(nodeRunResult: workflowNodeRunResult);
        }
        catch (BusinessException ex)
        {
            workflowNodeRunResult.Status = WorkflowNodeRunStatus.Exception;
            workflowNodeRunResult.ErrorMessage = ex.Message;
            workflowNodeRunResult.EndTime = DateTimeOffset.Now;

            workflowContext.Status = WorkflowRunStatus.Exception;
            logger.LogError(ex, "WorkflowEngine StartAsync BusinessException");
        }
        catch (Exception ex)
        {
            workflowNodeRunResult.Status = WorkflowNodeRunStatus.Exception;
            workflowNodeRunResult.ErrorMessage = ex.Message;
            workflowNodeRunResult.EndTime = DateTimeOffset.Now;

            workflowContext.Status = WorkflowRunStatus.Exception;
            logger.LogError(ex, "WorkflowEngine StartAsync Exception");
        }

        await workflowContext.OnNodeRunResultChangedAsync(workflowNodeRunResult);
        await workflowContext.OnWorkflowRunStatusChangedAsync(workflowContext.Status);
    }
}
