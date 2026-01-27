using System.Text.Json;
using MoAI.Workflow.Enums;
using MoAI.Workflow.Models;
using MoAI.Workflow.Services;

namespace MoAI.Workflow.Runtime;

/// <summary>
/// 工作流上下文管理器，负责管理工作流执行期间的上下文状态.
/// 包括上下文初始化、更新和变量映射维护.
/// </summary>
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
            RuntimeParameters = new Dictionary<string, object>(startupParameters),
            ExecutedNodeKeys = new HashSet<string>(),
            NodePipelines = new Dictionary<string, INodePipeline>(),
            FlattenedVariables = new Dictionary<string, object>()
        };

        // 添加系统变量到扁平化变量映射
        if (systemVariables != null)
        {
            foreach (var kvp in systemVariables)
            {
                context.FlattenedVariables[$"sys.{kvp.Key}"] = kvp.Value;
            }
        }

        // 添加启动参数到扁平化变量映射
        foreach (var kvp in startupParameters)
        {
            context.FlattenedVariables[$"input.{kvp.Key}"] = kvp.Value;
        }

        return context;
    }

    /// <summary>
    /// 更新工作流上下文，记录节点执行结果.
    /// </summary>
    /// <param name="context">工作流上下文.</param>
    /// <param name="nodeKey">节点键.</param>
    /// <param name="nodeDefine">节点定义.</param>
    /// <param name="inputs">节点输入.</param>
    /// <param name="outputs">节点输出.</param>
    /// <param name="state">节点状态.</param>
    /// <param name="errorMessage">错误消息（可选）.</param>
    public void UpdateContext(
        WorkflowContext context,
        string nodeKey,
        INodeDefine nodeDefine,
        Dictionary<string, object> inputs,
        Dictionary<string, object> outputs,
        NodeState state,
        string? errorMessage = null)
    {
        // 标记节点已执行
        context.ExecutedNodeKeys.Add(nodeKey);

        // 将输入和输出转换为 JsonElement
        var inputJsonElement = JsonSerializer.SerializeToElement(inputs);
        var outputJsonElement = JsonSerializer.SerializeToElement(outputs);

        // 创建节点管道
        var pipeline = new NodePipeline
        {
            NodeDefine = nodeDefine,
            State = state,
            InputJsonElement = inputJsonElement,
            InputJsonMap = inputs,
            OutputJsonElement = outputJsonElement,
            OutputJsonMap = outputs,
            ErrorMessage = errorMessage ?? string.Empty
        };

        // 添加到节点管道映射
        context.NodePipelines[nodeKey] = pipeline;

        // 更新扁平化变量映射（仅在节点成功完成时）
        if (state == NodeState.Completed)
        {
            UpdateFlattenedVariables(context, nodeKey, outputJsonElement);
        }
    }

    /// <summary>
    /// 更新扁平化变量映射，将节点输出添加到上下文.
    /// </summary>
    /// <param name="context">工作流上下文.</param>
    /// <param name="nodeKey">节点键.</param>
    /// <param name="outputJsonElement">节点输出的 JSON 元素.</param>
    private void UpdateFlattenedVariables(
        WorkflowContext context,
        string nodeKey,
        JsonElement outputJsonElement)
    {
        // 使用 VariableResolutionService 扁平化输出
        var flattenedOutput = _variableResolutionService.FlattenJson(nodeKey, outputJsonElement);

        // 将扁平化的输出添加到上下文的扁平化变量映射
        foreach (var kvp in flattenedOutput)
        {
            context.FlattenedVariables[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>
    /// 获取上下文中所有可用的变量键.
    /// </summary>
    /// <param name="context">工作流上下文.</param>
    /// <returns>所有可用的变量键列表.</returns>
    public List<string> GetAvailableVariableKeys(WorkflowContext context)
    {
        return context.FlattenedVariables.Keys.ToList();
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

        // 从扁平化变量映射中移除该节点的所有变量
        var keysToRemove = context.FlattenedVariables.Keys
            .Where(k => k.StartsWith($"{nodeKey}."))
            .ToList();

        foreach (var key in keysToRemove)
        {
            context.FlattenedVariables.Remove(key);
        }
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
    public Dictionary<string, object> RuntimeParameters { get; set; } = new();

    /// <inheritdoc/>
    public HashSet<string> ExecutedNodeKeys { get; set; } = new();

    /// <inheritdoc/>
    public Dictionary<string, INodePipeline> NodePipelines { get; set; } = new();

    /// <inheritdoc/>
    public Dictionary<string, object> FlattenedVariables { get; set; } = new();
}

/// <summary>
/// 节点管道实现类.
/// </summary>
internal class NodePipeline : INodePipeline
{
    /// <inheritdoc/>
    public required INodeDefine NodeDefine { get; init; }

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
}
