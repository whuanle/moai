using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Infra.Exceptions;
using MoAI.Workflow.Enums;
using MoAI.Workflow.Models;
using MoAI.Workflow.Services;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace MoAI.Workflow.Runtime;

/// <summary>
/// 工作流运行时，负责编排和执行工作流.
/// 实现节点顺序执行、流式传输结果和错误处理.
/// </summary>
[InjectOnTransient]
public class WorkflowRuntime
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly WorkflowContextManager _contextManager;
    private readonly ExpressionEvaluationService _expressionEvaluationService;
    private readonly WorkflowDefinitionService _workflowDefinitionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowRuntime"/> class.
    /// </summary>
    public WorkflowRuntime(
        IServiceScopeFactory serviceScopeFactory,
        WorkflowContextManager contextManager,
        ExpressionEvaluationService expressionEvaluationService,
        WorkflowDefinitionService workflowDefinitionService)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _contextManager = contextManager;
        _expressionEvaluationService = expressionEvaluationService;
        _workflowDefinitionService = workflowDefinitionService;
    }

    /// <summary>
    /// 执行工作流.
    /// 从 Start 节点开始，按照节点连接顺序执行所有节点，并实时流式传输执行结果.
    /// </summary>
    /// <param name="definition">工作流定义对象.</param>
    /// <param name="startupParameters">启动参数.</param>
    /// <param name="instanceId">工作流实例 ID.</param>
    /// <param name="systemVariables">系统变量（可选）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步流，产生 WorkflowProcessingItem 对象.</returns>
    public async IAsyncEnumerable<WorkflowProcessingItem> ExecuteAsync(
        WorkflowDefinition definition,
        Dictionary<string, object> startupParameters,
        string instanceId,
        Dictionary<string, object>? systemVariables = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 1. 验证工作流定义
        _workflowDefinitionService.ValidateWorkflowDefinition(definition);

        // 2. 获取节点设计列表
        var nodeDesigns = definition.Nodes.ToList();

        // 3. 初始化工作流上下文
        var context = _contextManager.InitializeContext(
            instanceId,
            definition.Id,
            startupParameters,
            systemVariables);

        // 4. 找到 Start 节点
        var startNode = nodeDesigns.FirstOrDefault(n => n.NodeType == NodeType.Start);
        if (startNode == null)
        {
            throw new BusinessException("工作流定义必须包含一个 Start 节点") { StatusCode = 400 };
        }

        // 5. 从 Start 节点开始执行
        string? currentNodeKey = startNode.NodeKey;
        var nodeDesignMap = nodeDesigns.ToDictionary(n => n.NodeKey);

        while (currentNodeKey != null && !cancellationToken.IsCancellationRequested)
        {
            // 获取当前节点设计
            if (!nodeDesignMap.TryGetValue(currentNodeKey, out var nodeDesign))
            {
                throw new BusinessException($"节点 {currentNodeKey} 不存在于工作流定义中") { StatusCode = 404 };
            }

            // 执行节点并流式传输结果
            WorkflowProcessingItem? processingItem = null;
            await foreach (var item in ExecuteNodeAsync(nodeDesign, context, cancellationToken))
            {
                processingItem = item;
                yield return item;
            }

            // 如果节点执行失败，终止工作流
            if (processingItem?.State == NodeState.Failed)
            {
                yield break;
            }

            // 如果是 End 节点，终止工作流
            if (nodeDesign.NodeType == NodeType.End)
            {
                yield break;
            }

            // 确定下一个节点
            currentNodeKey = DetermineNextNode(nodeDesign, context);
        }
    }

    /// <summary>
    /// 执行单个节点.
    /// </summary>
    private async IAsyncEnumerable<WorkflowProcessingItem> ExecuteNodeAsync(
        NodeDesign nodeDesign,
        WorkflowContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var nodeKey = nodeDesign.NodeKey;
        var nodeType = nodeDesign.NodeType;

        // 创建节点管道（包含参数上下文）
        var pipeline = _contextManager.CreateNodePipeline(context);

        Dictionary<string, object> inputs = new();
        NodeExecutionResult? result = null;
        WorkflowProcessingItem? errorItem = null;

        // 1. 流式传输节点开始执行状态
        yield return new WorkflowProcessingItem
        {
            NodeType = nodeType.ToString(),
            NodeKey = nodeKey,
            Input = new Dictionary<string, object>(),
            Output = new Dictionary<string, object>(),
            State = NodeState.Running,
            ErrorMessage = string.Empty,
            ExecutedTime = DateTimeOffset.Now
        };

        // 3. 获取节点运行时（每次创建新的作用域）
        using var scope = _serviceScopeFactory.CreateScope();
        var nodeRuntime = scope.ServiceProvider.GetKeyedService<INodeRuntime>(nodeType);

        try
        {
            // 2. 解析节点输入（从节点管道获取参数）
            inputs = ResolveNodeInputs(nodeDesign, pipeline);

            if (nodeRuntime == null)
            {
                throw new InvalidOperationException($"No runtime registered for node type: {nodeType}");
            }

            // 4. 执行节点（传递节点管道而不是工作流上下文）
            result = await nodeRuntime.ExecuteAsync(inputs, pipeline, cancellationToken);

            // 5. 更新节点管道结果
            pipeline = _contextManager.UpdatePipelineResult(
                pipeline,
                inputs,
                result.Output,
                result.State,
                result.ErrorMessage);

            // 6. 更新工作流上下文
            _contextManager.UpdateContext(context, nodeKey, pipeline);
        }
        catch (Exception ex)
        {
            // 捕获异常并准备错误信息
            var errorMessage = $"{ex.Message}\n{ex.StackTrace}";

            // 更新节点管道为失败状态
            pipeline = _contextManager.UpdatePipelineResult(
                pipeline,
                inputs,
                new Dictionary<string, object>(),
                NodeState.Failed,
                errorMessage);

            // 更新工作流上下文
            _contextManager.UpdateContext(context, nodeKey, pipeline);

            // 准备错误项
            errorItem = new WorkflowProcessingItem
            {
                NodeType = nodeType.ToString(),
                NodeKey = nodeKey,
                Input = inputs,
                Output = new Dictionary<string, object>(),
                State = NodeState.Failed,
                ErrorMessage = errorMessage,
                ExecutedTime = DateTimeOffset.Now
            };
        }

        // 7. 流式传输节点完成状态
        if (errorItem != null)
        {
            yield return errorItem;
        }
        else if (result != null)
        {
            yield return new WorkflowProcessingItem
            {
                NodeType = nodeType.ToString(),
                NodeKey = nodeKey,
                Input = inputs,
                Output = result.Output,
                State = result.State,
                ErrorMessage = result.ErrorMessage ?? string.Empty,
                ExecutedTime = DateTimeOffset.Now
            };
        }
    }

    /// <summary>
    /// 解析节点输入，评估所有字段表达式.
    /// </summary>
    private Dictionary<string, object> ResolveNodeInputs(
        NodeDesign nodeDesign,
        INodePipeline pipeline)
    {
        var inputs = new Dictionary<string, object>();

        foreach (var fieldDesign in nodeDesign.InputFieldDesigns)
        {
            var fieldName = fieldDesign.Key;
            var fieldConfig = fieldDesign.Value;

            try
            {
                // 评估字段表达式（从节点管道获取参数）
                var value = _expressionEvaluationService.EvaluateExpression(
                    fieldConfig.Value,
                    fieldConfig.ExpressionType,
                    pipeline);

                inputs[fieldName] = value;
            }
            catch (Exception ex)
            {
                throw new BusinessException(
                    $"节点 {nodeDesign.NodeKey} 的字段 {fieldName} 解析失败: {ex.Message}")
                {
                    StatusCode = 400
                };
            }
        }

        return inputs;
    }

    /// <summary>
    /// 确定下一个要执行的节点.
    /// </summary>
    private static string? DetermineNextNode(NodeDesign nodeDesign, IWorkflowContext context)
    {
        // 对于 Condition 节点，需要根据条件结果确定下一个节点
        if (nodeDesign.NodeType == NodeType.Condition)
        {
            return DetermineConditionNextNode(nodeDesign, context);
        }

        // 对于 ForEach 节点，需要根据循环状态确定下一个节点
        if (nodeDesign.NodeType == NodeType.ForEach)
        {
            return DetermineForEachNextNode(nodeDesign, context);
        }

        // 对于 Fork 节点，需要等待所有分支完成后确定下一个节点
        if (nodeDesign.NodeType == NodeType.Fork)
        {
            return DetermineForkNextNode(nodeDesign, context);
        }

        // 对于一般节点，返回第一个下游节点
        if (nodeDesign.NextNodeKeys != null && nodeDesign.NextNodeKeys.Count > 0)
        {
            return nodeDesign.NextNodeKeys.First();
        }

        // 没有下一个节点
        return null;
    }

    /// <summary>
    /// 确定 Condition 节点的下一个节点.
    /// </summary>
    private static string? DetermineConditionNextNode(NodeDesign nodeDesign, IWorkflowContext context)
    {
        // 从节点输出中获取条件结果
        if (context.NodePipelines.TryGetValue(nodeDesign.NodeKey, out var pipeline))
        {
            if (pipeline.OutputJsonMap.TryGetValue("result", out var resultObj) && resultObj is bool result)
            {
                // 根据条件结果选择分支
                if (nodeDesign.NextNodeKeys != null && nodeDesign.NextNodeKeys.Count >= 2)
                {
                    return result ? nodeDesign.NextNodeKeys.ElementAt(0) : nodeDesign.NextNodeKeys.ElementAt(1);
                }
                else if (nodeDesign.NextNodeKeys != null && nodeDesign.NextNodeKeys.Count == 1)
                {
                    return nodeDesign.NextNodeKeys.First();
                }
            }
        }

        return nodeDesign.NextNodeKeys?.FirstOrDefault();
    }

    /// <summary>
    /// 确定 ForEach 节点的下一个节点.
    /// </summary>
    private static string? DetermineForEachNextNode(NodeDesign nodeDesign, IWorkflowContext context)
    {
        return nodeDesign.NextNodeKeys?.FirstOrDefault();
    }

    /// <summary>
    /// 确定 Fork 节点的下一个节点.
    /// </summary>
    private static string? DetermineForkNextNode(NodeDesign nodeDesign, IWorkflowContext context)
    {
        return nodeDesign.NextNodeKeys?.FirstOrDefault();
    }
}
