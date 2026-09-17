using System.Text.Json.Nodes;
using MoAI.App.Workflow.DataTransfer;
using MoAI.App.Workflow.Definition;
using MoAI.App.Workflow.Events;
using MoAI.App.Workflow.Nodes;
using MoAI.App.Workflow.Persistence;

namespace MoAI.App.Workflow.Instance;

/// <summary>
/// 工作流调度器 - 引擎核心.
/// 按 DAG 图执行流程实例：就绪节点调度、条件分支路由、跳过传播、并行分支汇合、检查点持久化.
/// 每个节点工作过程都会推送执行事件（IWorkflowEventPublisher），并在节点状态变更后落库（支持流程恢复）.
/// </summary>
public class WorkflowScheduler
{
    private readonly INodeExecutorRegistry _nodeExecutorRegistry;
    private readonly InputResolver _inputResolver;
    private readonly IWorkflowEventPublisher _eventPublisher;
    private readonly IWorkflowInstanceStore _instanceStore;
    private readonly SemaphoreSlim _checkpointLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowScheduler"/> class.
    /// </summary>
    public WorkflowScheduler(
        INodeExecutorRegistry nodeExecutorRegistry,
        InputResolver inputResolver,
        IWorkflowEventPublisher eventPublisher,
        IWorkflowInstanceStore instanceStore)
    {
        _nodeExecutorRegistry = nodeExecutorRegistry;
        _inputResolver = inputResolver;
        _eventPublisher = eventPublisher;
        _instanceStore = instanceStore;
    }

    /// <summary>
    /// 边的运行时状态.
    /// </summary>
    private enum EdgeState
    {
        /// <summary>上游未完成.</summary>
        Pending,

        /// <summary>上游已完成，控制流沿此边流动.</summary>
        Done,

        /// <summary>此边被跳过（条件分支未命中或上游链路整体跳过）.</summary>
        Skipped,
    }

    /// <summary>
    /// 执行流程实例直到完成、挂起或取消.
    /// 支持从任意检查点续跑：<paramref name="instance"/> 可携带已恢复的节点状态.
    /// </summary>
    /// <returns>终态实例（Completed / Suspended / Cancelled）.</returns>
    public async Task<WorkflowInstance> RunAsync(WorkflowInstance instance, CompiledWorkflow graph, CancellationToken cancellationToken)
    {
        // 边状态：首次执行全部 Pending；恢复时从已完成节点重放路由推导
        var edgeStates = graph.Definition.Connections.ToDictionary(c => c.Id, _ => EdgeState.Pending);
        ReplayCompletedNodeRouting(instance, graph, edgeStates);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var readyNodes = CollectReadyNodes(instance, graph, edgeStates);
            if (readyNodes.Count == 0)
            {
                break;
            }

            // 就绪节点并行执行（Fork/汇合语义）；单失败即挂起，已完成节点的输出保留用于恢复
            var executions = readyNodes.Select(node => ExecuteNodeAsync(instance, graph, node, edgeStates, cancellationToken));
            var results = await Task.WhenAll(executions);

            if (results.Any(r => !r))
            {
                instance.Status = InstanceStatus.Suspended;
                instance.ErrorMessage ??= "存在执行失败的节点，实例已挂起，可修复后恢复";
                instance.UpdatedAt = DateTimeOffset.Now;
                await _instanceStore.SaveAsync(instance, cancellationToken);
                await PublishAsync(new WorkflowSuspendedEvent
                {
                    InstanceId = instance.Id,
                    NodeKey = results.Select((ok, i) => (ok, i)).Where(x => !x.ok)
                        .Select(x => readyNodes[x.i].Key).FirstOrDefault(),
                    Reason = instance.ErrorMessage,
                }, cancellationToken);
                return instance;
            }
        }

        // 无失败且无就绪节点：检查是否存在失败/跳过导致的提前终止
        var failedNode = instance.NodeStates.Values.FirstOrDefault(n => n.State == NodeState.Failed);
        if (failedNode != null)
        {
            instance.Status = InstanceStatus.Suspended;
            instance.UpdatedAt = DateTimeOffset.Now;
            await _instanceStore.SaveAsync(instance, cancellationToken);
            return instance;
        }

        instance.Status = InstanceStatus.Completed;
        instance.Output ??= new JsonObject();
        instance.EndedAt = DateTimeOffset.Now;
        instance.UpdatedAt = DateTimeOffset.Now;
        await _instanceStore.SaveAsync(instance, cancellationToken);
        await PublishAsync(new WorkflowCompletedEvent
        {
            InstanceId = instance.Id,
            Output = instance.Output,
        }, cancellationToken);
        return instance;
    }

    /// <summary>
    /// 收集就绪节点：Pending 且（是开始节点 或 全部入边已解析且至少一条已完成）.
    /// </summary>
    private static List<NodeDefinition> CollectReadyNodes(
        WorkflowInstance instance,
        CompiledWorkflow graph,
        Dictionary<string, EdgeState> edgeStates)
    {
        var ready = new List<NodeDefinition>();
        foreach (var node in graph.Definition.Nodes)
        {
            if (instance.NodeStates.TryGetValue(node.Key, out var state) && state.State != NodeState.Pending)
            {
                continue;
            }

            if (!graph.IncomingEdges.TryGetValue(node.Key, out var incoming) || incoming.Count == 0)
            {
                // 开始节点（无入边）
                ready.Add(node);
                continue;
            }

            var resolved = true;
            var hasDone = false;
            foreach (var edge in incoming)
            {
                var edgeState = edgeStates[edge.Id];
                if (edgeState == EdgeState.Pending)
                {
                    resolved = false;
                    break;
                }

                hasDone |= edgeState == EdgeState.Done;
            }

            if (resolved && hasDone)
            {
                ready.Add(node);
            }
        }

        return ready;
    }

    /// <summary>
    /// 执行单个节点：解析输入 → 调用执行器 → 记录检查点 → 推送事件 → 更新边状态.
    /// 返回 false 表示节点失败（实例将被挂起）.
    /// </summary>
    private async Task<bool> ExecuteNodeAsync(
        WorkflowInstance instance,
        CompiledWorkflow graph,
        NodeDefinition node,
        Dictionary<string, EdgeState> edgeStates,
        CancellationToken cancellationToken)
    {
        var state = instance.NodeStates[node.Key];
        state.Attempts++;
        state.State = NodeState.Running;
        state.StartedAt = DateTimeOffset.Now;
        state.ErrorMessage = null;
        await _instanceStore.SaveAsync(instance, cancellationToken);
        await PublishAsync(new NodeStateChangedEvent
        {
            InstanceId = instance.Id,
            NodeKey = node.Key,
            NodeType = node.Type,
            NodeName = node.Name,
            State = NodeState.Running,
            Attempt = state.Attempts,
        }, cancellationToken);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var scope = BuildVariableScope(instance, graph);
            var inputs = node.Type == NodeTypes.Start
                ? instance.Input.CloneObject()
                : _inputResolver.Resolve(node, scope);
            state.Input = inputs;

            var executor = _nodeExecutorRegistry.Get(node.Type);
            if (executor == null)
            {
                throw new WorkflowException($"节点类型 {node.Type} 未注册执行器（NodeExecutorRegistry）");
            }

            var context = new NodeExecutionContext(instance.Id, node, inputs, scope, _eventPublisher);
            var result = await executor.ExecuteAsync(context, cancellationToken);

            if (result.State != NodeState.Completed)
            {
                await MarkNodeFailedAsync(instance, graph, node, state, edgeStates, result.ErrorMessage ?? "节点执行失败", stopwatch);
                return false;
            }

            state.State = NodeState.Completed;
            state.Output = result.Output;
            state.EndedAt = DateTimeOffset.Now;
            await SaveCheckpointAsync(instance, cancellationToken);
            stopwatch.Stop();
            await PublishAsync(new NodeStateChangedEvent
            {
                InstanceId = instance.Id,
                NodeKey = node.Key,
                NodeType = node.Type,
                NodeName = node.Name,
                State = NodeState.Completed,
                Input = state.Input,
                Output = state.Output,
                Attempt = state.Attempts,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
            }, cancellationToken);

            // 结束节点：输出即工作流最终输出
            if (node.Type == NodeTypes.End)
            {
                instance.Output = result.Output.CloneObject();
            }

            RouteOutgoingEdges(graph, node, state, edgeStates);
            PropagateSkips(instance, graph, edgeStates);
            return true;
        }
        catch (OperationCanceledException)
        {
            state.State = NodeState.Pending;
            state.EndedAt = DateTimeOffset.Now;
            await SaveCheckpointAsync(instance, CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            await MarkNodeFailedAsync(instance, graph, node, state, edgeStates, ex.Message, stopwatch);
            return false;
        }
    }

    /// <summary>
    /// 标记节点失败：更新状态、检查点落库、推送事件、下游入边置为跳过（阻断后续执行）.
    /// </summary>
    private async Task MarkNodeFailedAsync(
        WorkflowInstance instance,
        CompiledWorkflow graph,
        NodeDefinition node,
        NodeExecutionState state,
        Dictionary<string, EdgeState> edgeStates,
        string errorMessage,
        System.Diagnostics.Stopwatch stopwatch)
    {
        state.State = NodeState.Failed;
        state.ErrorMessage = errorMessage;
        state.EndedAt = DateTimeOffset.Now;
        await SaveCheckpointAsync(instance, CancellationToken.None);
        stopwatch.Stop();
        instance.ErrorMessage = $"节点 {node.Key}({node.Name}) 执行失败：{errorMessage}";
        await PublishAsync(new NodeStateChangedEvent
        {
            InstanceId = instance.Id,
            NodeKey = node.Key,
            NodeType = node.Type,
            NodeName = node.Name,
            State = NodeState.Failed,
            Input = state.Input,
            ErrorMessage = errorMessage,
            Attempt = state.Attempts,
            ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
        }, CancellationToken.None);
    }

    /// <summary>
    /// 节点完成后路由出边：条件节点按输出结果选择分支，其余节点激活全部出边.
    /// </summary>
    private static void RouteOutgoingEdges(
        CompiledWorkflow graph,
        NodeDefinition node,
        NodeExecutionState state,
        Dictionary<string, EdgeState> edgeStates)
    {
        if (!graph.OutgoingEdges.TryGetValue(node.Key, out var outgoing))
        {
            return;
        }

        if (node.Type == NodeTypes.Condition)
        {
            var conditionResult = state.Output != null
                && state.Output.TryGetPropertyValue("result", out var resultNode)
                && resultNode is JsonValue value
                && value.TryGetValue<bool>(out var boolValue)
                && boolValue;

            var matched = conditionResult ? "true" : "false";
            foreach (var edge in outgoing)
            {
                edgeStates[edge.Id] = edge.Condition == matched ? EdgeState.Done : EdgeState.Skipped;
            }

            return;
        }

        if (node.Type == NodeTypes.Switch)
        {
            // 多条件节点：输出 result 为命中的分支 id（或 else），按字符串匹配出边
            var switchMatched = state.Output != null
                && state.Output.TryGetPropertyValue("result", out var switchResultNode)
                && switchResultNode is JsonValue switchValue
                && switchValue.TryGetValue<string>(out var switchString)
                ? switchString
                : null;

            foreach (var edge in outgoing)
            {
                edgeStates[edge.Id] = !string.IsNullOrEmpty(switchMatched) && edge.Condition == switchMatched ? EdgeState.Done : EdgeState.Skipped;
            }

            return;
        }

        foreach (var edge in outgoing)
        {
            edgeStates[edge.Id] = EdgeState.Done;
        }
    }

    /// <summary>
    /// 跳过传播：入边全部被跳过的 Pending 节点标记为 Skipped，并继续向下游传播.
    /// 已经是 Skipped 的节点也会继续把出边标记为跳过（断点恢复重放时，已跳过节点没有 Pending 状态可供触发传播）.
    /// </summary>
    private static void PropagateSkips(
        WorkflowInstance instance,
        CompiledWorkflow graph,
        Dictionary<string, EdgeState> edgeStates)
    {
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var node in graph.Definition.Nodes)
            {
                var nodeState = instance.NodeStates[node.Key].State;

                if (nodeState == NodeState.Skipped)
                {
                    if (graph.OutgoingEdges.TryGetValue(node.Key, out var skippedOutgoing))
                    {
                        foreach (var edge in skippedOutgoing)
                        {
                            if (edgeStates[edge.Id] != EdgeState.Skipped)
                            {
                                edgeStates[edge.Id] = EdgeState.Skipped;
                                changed = true;
                            }
                        }
                    }

                    continue;
                }

                if (nodeState != NodeState.Pending)
                {
                    continue;
                }

                if (!graph.IncomingEdges.TryGetValue(node.Key, out var incoming) || incoming.Count == 0)
                {
                    continue;
                }

                if (incoming.All(e => edgeStates[e.Id] == EdgeState.Skipped))
                {
                    instance.NodeStates[node.Key].State = NodeState.Skipped;
                    if (graph.OutgoingEdges.TryGetValue(node.Key, out var outgoing))
                    {
                        foreach (var edge in outgoing)
                        {
                            if (edgeStates[edge.Id] != EdgeState.Skipped)
                            {
                                edgeStates[edge.Id] = EdgeState.Skipped;
                                changed = true;
                            }
                        }
                    }

                    changed = true;
                }
            }
        }
    }

    /// <summary>
    /// 恢复执行时，按拓扑序重放已完成节点的路由，重建边状态（无需单独持久化边表）.
    /// </summary>
    private void ReplayCompletedNodeRouting(
        WorkflowInstance instance,
        CompiledWorkflow graph,
        Dictionary<string, EdgeState> edgeStates)
    {
        // 恢复时把中断的节点重置为待执行：Running（崩溃时正在执行）与 Failed（失败重试）都会重跑，Attempts 保留
        foreach (var state in instance.NodeStates.Values.Where(n => n.State is NodeState.Running or NodeState.Failed))
        {
            state.State = NodeState.Pending;
        }

        // 按拓扑序重放已完成节点：Kahn 排序
        var ordered = TopologicalSort(graph);
        foreach (var node in ordered)
        {
            if (instance.NodeStates.TryGetValue(node.Key, out var state) && state.State == NodeState.Completed)
            {
                RouteOutgoingEdges(graph, node, state, edgeStates);
            }
        }

        PropagateSkips(instance, graph, edgeStates);
    }

    /// <summary>
    /// 对图做拓扑排序（Kahn 算法），用于恢复时按序重放路由.
    /// </summary>
    private static List<NodeDefinition> TopologicalSort(CompiledWorkflow graph)
    {
        var inDegree = graph.Definition.Nodes.ToDictionary(n => n.Key, _ => 0);
        foreach (var connection in graph.Definition.Connections)
        {
            inDegree[connection.Target]++;
        }

        var result = new List<NodeDefinition>();
        var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var nodeMap = graph.NodeMap;
        while (queue.Count > 0)
        {
            var key = queue.Dequeue();
            result.Add(nodeMap[key]);
            if (!graph.OutgoingEdges.TryGetValue(key, out var outgoing))
            {
                continue;
            }

            foreach (var edge in outgoing)
            {
                inDegree[edge.Target]--;
                if (inDegree[edge.Target] == 0)
                {
                    queue.Enqueue(edge.Target);
                }
            }
        }

        // 图无环（验证器保证），但防御性补充未覆盖节点
        var sorted = result.Select(n => n.Key).ToHashSet();
        result.AddRange(graph.Definition.Nodes.Where(n => !sorted.Contains(n.Key)));
        return result;
    }

    /// <summary>
    /// 构建变量作用域：sys.* + input.* + 已完成节点输出（流程数据传输的上下文）.
    /// </summary>
    private static WorkflowVariableScope BuildVariableScope(WorkflowInstance instance, CompiledWorkflow graph)
    {
        var systemVariables = new JsonObject
        {
            ["instanceId"] = instance.Id,
            ["workflowId"] = instance.DefinitionId,
            ["workflowName"] = instance.DefinitionName,
            ["startedAt"] = instance.StartedAt?.ToString("O"),
        };

        var nodeOutputs = new Dictionary<string, JsonObject>();
        foreach (var (nodeKey, state) in instance.NodeStates)
        {
            if (state.State == NodeState.Completed && state.Output != null)
            {
                nodeOutputs[nodeKey] = state.Output.CloneObject();
            }
        }

        return new WorkflowVariableScope(systemVariables, instance.Input, nodeOutputs, instance.SystemVariables);
    }

    /// <summary>
    /// 检查点：实例状态变更后立即落库（流程恢复的依据）.
    /// </summary>
    private async Task SaveCheckpointAsync(WorkflowInstance instance, CancellationToken cancellationToken)
    {
        instance.UpdatedAt = DateTimeOffset.Now;
        await _checkpointLock.WaitAsync(cancellationToken);
        try
        {
            await _instanceStore.SaveAsync(instance, cancellationToken);
        }
        finally
        {
            _checkpointLock.Release();
        }
    }

    private Task PublishAsync(WorkflowEvent workflowEvent, CancellationToken cancellationToken)
    {
        return _eventPublisher.PublishAsync(workflowEvent, cancellationToken);
    }
}
