using Maomi;
using MoAI.Workflow.Enums;
using MoAI.Workflow.Models;
using MoAI.Workflow.Services;
using System.Text.Json;

namespace MoAI.Workflow.Runtime;

/// <summary>
/// 工作流上下文管理器，负责管理工作流执行期间的上下文状态.
/// 包括上下文初始化、节点管道创建和上下文更新.
/// </summary>
[InjectOnScoped]
public class WorkflowContextManager
{
    private readonly VariableResolutionService _variableResolutionService;

    /// <summary>
    /// 初始化 WorkflowContextManager 实例.
    /// </summary>
    /// <param name="variableResolutionService">变量解析服务.</param>
    public WorkflowContextManager(VariableResolutionService variableResolutionService)
    {
        _variableResolutionService = variableResolutionService;
    }

    /// <summary>
    /// 初始化工作流上下文.
    /// </summary>
    /// <param name="instanceId">工作流实例 ID.</param>
    /// <param name="definitionId">工作流定义 ID.</param>
    /// <param name="startupParameters">启动参数.</param>
    /// <param name="systemVariables">系统变量（可选）.</param>
    /// <returns>初始化后的工作流上下文.</returns>
    public WorkflowContext InitializeContext(
        string instanceId,
        string definitionId,
        Dictionary<string, object> startupParameters,
        Dictionary<string, object>? systemVariables = null)
    {
        var context = new WorkflowContext
        {
            InstanceId = instanceId,
            DefinitionId = definitionId,
            SystemVariables = systemVariables != null
                ? new Dictionary<string, object>(systemVariables)
                : new Dictionary<string, object>(),
            RuntimeParameters = new Dictionary<string, object>(startupParameters),
            ExecutedNodeKeys = new HashSet<string>(),
            NodePipelines = new Dictionary<string, INodePipeline>()
        };

        return context;
    }

    /// <summary>
    /// 为节点执行创建节点管道.
    /// 节点管道包含节点执行所需的所有参数上下文.
    /// </summary>
    /// <param name="context">工作流上下文.</param>
    /// <returns>节点管道.</returns>
    public NodePipeline CreateNodePipeline(WorkflowContext context)
    {
        // 构建扁平化变量映射
        var flattenedVariables = new Dictionary<string, object>();

        // 1. 添加系统变量 (sys.*)
        foreach (var kvp in context.SystemVariables)
        {
            flattenedVariables[$"sys.{kvp.Key}"] = kvp.Value;
        }

        // 2. 添加启动参数 (input.*)
        foreach (var kvp in context.RuntimeParameters)
        {
            flattenedVariables[$"input.{kvp.Key}"] = kvp.Value;
        }

        // 3. 添加已执行节点的输出 (nodeKey.*)
        var nodeOutputs = new Dictionary<string, Dictionary<string, object>>();
        foreach (var nodeKey in context.ExecutedNodeKeys)
        {
            if (context.NodePipelines.TryGetValue(nodeKey, out var executedPipeline))
            {
                // 复制节点输出
                nodeOutputs[nodeKey] = new Dictionary<string, object>(executedPipeline.OutputJsonMap);

                // 扁平化节点输出到变量映射
                var flattenedOutput = _variableResolutionService.FlattenJson(
                    nodeKey,
                    executedPipeline.OutputJsonElement);

                foreach (var kvp in flattenedOutput)
                {
                    flattenedVariables[kvp.Key] = kvp.Value;
                }
            }
        }

        return new NodePipeline
        {
            State = NodeState.Pending,
            InputJsonElement = default,
            InputJsonMap = new Dictionary<string, object>(),
            OutputJsonElement = default,
            OutputJsonMap = new Dictionary<string, object>(),
            ErrorMessage = string.Empty,
            FlattenedVariables = flattenedVariables,
            SystemVariables = new Dictionary<string, object>(context.SystemVariables),
            RuntimeParameters = new Dictionary<string, object>(context.RuntimeParameters),
            NodeOutputs = nodeOutputs
        };
    }

    /// <summary>
    /// 更新工作流上下文，记录节点执行结果.
    /// </summary>
    /// <param name="context">工作流上下文.</param>
    /// <param name="nodeKey">节点键.</param>
    /// <param name="pipeline">节点管道（包含执行结果）.</param>
    public void UpdateContext(
        WorkflowContext context,
        string nodeKey,
        NodePipeline pipeline)
    {
        // 标记节点已执行
        context.ExecutedNodeKeys.Add(nodeKey);

        // 添加到节点管道映射
        context.NodePipelines[nodeKey] = pipeline;
    }

    /// <summary>
    /// 更新节点管道的执行结果.
    /// </summary>
    /// <param name="pipeline">节点管道.</param>
    /// <param name="inputs">节点输入.</param>
    /// <param name="outputs">节点输出.</param>
    /// <param name="state">节点状态.</param>
    /// <param name="errorMessage">错误消息（可选）.</param>
    /// <returns>更新后的节点管道.</returns>
    public NodePipeline UpdatePipelineResult(
        NodePipeline pipeline,
        Dictionary<string, object> inputs,
        Dictionary<string, object> outputs,
        NodeState state,
        string? errorMessage = null)
    {
        // 将输入和输出转换为 JsonElement
        var inputJsonElement = JsonSerializer.SerializeToElement(inputs);
        var outputJsonElement = JsonSerializer.SerializeToElement(outputs);

        return new NodePipeline
        {
            State = state,
            InputJsonElement = inputJsonElement,
            InputJsonMap = inputs,
            OutputJsonElement = outputJsonElement,
            OutputJsonMap = outputs,
            ErrorMessage = errorMessage ?? string.Empty,
            FlattenedVariables = pipeline.FlattenedVariables,
            SystemVariables = pipeline.SystemVariables,
            RuntimeParameters = pipeline.RuntimeParameters,
            NodeOutputs = pipeline.NodeOutputs
        };
    }

    /// <summary>
    /// 获取上下文中所有可用的变量键.
    /// </summary>
    /// <param name="context">工作流上下文.</param>
    /// <returns>所有可用的变量键列表.</returns>
    public List<string> GetAvailableVariableKeys(WorkflowContext context)
    {
        var keys = new List<string>();

        // 系统变量
        foreach (var kvp in context.SystemVariables)
        {
            keys.Add($"sys.{kvp.Key}");
        }

        // 启动参数
        foreach (var kvp in context.RuntimeParameters)
        {
            keys.Add($"input.{kvp.Key}");
        }

        // 节点输出
        foreach (var nodeKey in context.ExecutedNodeKeys)
        {
            if (context.NodePipelines.TryGetValue(nodeKey, out var pipeline))
            {
                foreach (var outputKey in pipeline.OutputJsonMap.Keys)
                {
                    keys.Add($"{nodeKey}.{outputKey}");
                }
            }
        }

        return keys;
    }

    /// <summary>
    /// 清除指定节点的执行记录（用于重试或回滚场景）.
    /// </summary>
    /// <param name="context">工作流上下文.</param>
    /// <param name="nodeKey">节点键.</param>
    public void ClearNodeExecution(WorkflowContext context, string nodeKey)
    {
        // 从已执行节点集合中移除
        context.ExecutedNodeKeys.Remove(nodeKey);

        // 从节点管道映射中移除
        context.NodePipelines.Remove(nodeKey);
    }
}

/// <summary>
/// 工作流上下文实现类.
/// </summary>
public class WorkflowContext : IWorkflowContext
{
    /// <inheritdoc/>
    public string InstanceId { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string DefinitionId { get; set; } = string.Empty;

    /// <inheritdoc/>
    public Dictionary<string, object> SystemVariables { get; set; } = new();

    /// <inheritdoc/>
    public Dictionary<string, object> RuntimeParameters { get; set; } = new();

    /// <inheritdoc/>
    public HashSet<string> ExecutedNodeKeys { get; set; } = new();

    /// <inheritdoc/>
    public Dictionary<string, INodePipeline> NodePipelines { get; set; } = new();
}

/// <summary>
/// 节点管道实现类.
/// </summary>
public class NodePipeline : INodePipeline
{
    /// <inheritdoc/>
    public NodeState State { get; init; }

    /// <inheritdoc/>
    public JsonElement InputJsonElement { get; init; }

    /// <inheritdoc/>
    public Dictionary<string, object> InputJsonMap { get; init; } = new();

    /// <inheritdoc/>
    public JsonElement OutputJsonElement { get; init; }

    /// <inheritdoc/>
    public Dictionary<string, object> OutputJsonMap { get; init; } = new();

    /// <inheritdoc/>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <inheritdoc/>
    public Dictionary<string, object> FlattenedVariables { get; init; } = new();

    /// <inheritdoc/>
    public Dictionary<string, object> SystemVariables { get; init; } = new();

    /// <inheritdoc/>
    public Dictionary<string, object> RuntimeParameters { get; init; } = new();

    /// <inheritdoc/>
    public Dictionary<string, Dictionary<string, object>> NodeOutputs { get; init; } = new();
}
