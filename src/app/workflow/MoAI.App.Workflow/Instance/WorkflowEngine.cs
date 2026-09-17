using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using MoAI.App.Workflow.Definition;
using MoAI.App.Workflow.Events;
using MoAI.App.Workflow.Nodes;
using MoAI.App.Workflow.Persistence;

namespace MoAI.App.Workflow.Instance;

/// <summary>
/// 工作流引擎门面.
/// 职责：
/// 1. 流程定义 - 加载已发布定义并编译成可执行图（配合 WorkflowValidator / WorkflowCompiler）；
/// 2. 流程实例 - 创建实例、驱动调度器执行、断点恢复（Resume）、取消；
/// 3. 执行观察 - 所有执行事件通过 IWorkflowEventPublisher 推送，并自动写入事件日志.
/// </summary>
public class WorkflowEngine
{
    private readonly IWorkflowDefinitionStore _definitionStore;
    private readonly IWorkflowInstanceStore _instanceStore;
    private readonly IWorkflowEventLogStore _eventLogStore;
    private readonly WorkflowCompiler _compiler;
    private readonly WorkflowScheduler _scheduler;
    private readonly IWorkflowEventPublisher _eventPublisher;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _runningInstances = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowEngine"/> class.
    /// </summary>
    public WorkflowEngine(
        IWorkflowDefinitionStore definitionStore,
        IWorkflowInstanceStore instanceStore,
        IWorkflowEventLogStore eventLogStore,
        WorkflowCompiler compiler,
        WorkflowScheduler scheduler,
        IWorkflowEventPublisher eventPublisher)
    {
        _definitionStore = definitionStore;
        _instanceStore = instanceStore;
        _eventLogStore = eventLogStore;
        _compiler = compiler;
        _scheduler = scheduler;
        _eventPublisher = eventPublisher;

        // 执行事件自动落日志库（恢复后仍可追溯执行历史）
        _eventPublisher.Subscribe(e => _eventLogStore.AppendAsync(e));
    }

    /// <summary>
    /// 启动工作流：加载已发布定义 → 编译 → 创建实例 → 执行到终态.
    /// </summary>
    /// <param name="definitionId">工作流定义 ID.</param>
    /// <param name="input">启动参数（开始节点的 run 输入）.</param>
    /// <param name="instanceId">实例 ID（不传自动生成）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>终态实例.</returns>
    public async Task<WorkflowInstance> StartAsync(
        string definitionId,
        JsonObject? input = null,
        JsonObject? systemVariables = null,
        string? instanceId = null,
        CancellationToken cancellationToken = default)
    {
        var definition = await _definitionStore.FindPublishedDefinitionByIdAsync(definitionId, cancellationToken)
            ?? throw new WorkflowException($"工作流定义 {definitionId} 不存在或未发布");

        return await StartWithDefinitionAsync(definition, input, systemVariables, instanceId, cancellationToken);
    }

    /// <summary>
    /// 用给定的定义对象直接启动工作流（不经定义存储查询），用于调试执行设计器草稿.
    /// 定义会先经过完整校验与编译；全局变量按「定义默认值 ← <paramref name="systemVariables"/> 传入值」合并.
    /// </summary>
    /// <param name="definition">工作流定义（可为未发布的草稿）.</param>
    /// <param name="input">启动参数（开始节点的 run 输入）.</param>
    /// <param name="systemVariables">全局变量实际值（键为变量名），未提供的变量使用定义默认值.</param>
    /// <param name="instanceId">实例 ID（不传自动生成）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>终态实例.</returns>
    public async Task<WorkflowInstance> StartWithDefinitionAsync(
        WorkflowDefinition definition,
        JsonObject? input = null,
        JsonObject? systemVariables = null,
        string? instanceId = null,
        CancellationToken cancellationToken = default)
    {
        var graph = _compiler.Compile(definition);
        var instance = new WorkflowInstance
        {
            Id = instanceId ?? Guid.NewGuid().ToString("N"),
            DefinitionId = definition.Id,
            DefinitionName = definition.Name,
            DefinitionVersion = definition.Version,
            Status = InstanceStatus.Created,
            Input = input?.CloneObject() ?? new JsonObject(),
            SystemVariables = MergeSystemVariables(definition, systemVariables),
            NodeStates = definition.Nodes.ToDictionary(
                n => n.Key,
                n => new NodeExecutionState
                {
                    NodeKey = n.Key,
                    NodeType = n.Type,
                    NodeName = n.Name,
                    State = NodeState.Pending,
                }),
        };

        await _instanceStore.CreateAsync(instance, cancellationToken);
        return await ExecuteAsync(instance, graph, cancellationToken, resumed: false);
    }

    /// <summary>
    /// 合并全局变量：定义声明的默认值先行，调用方传入值覆盖；未在定义中声明的传入值也保留.
    /// </summary>
    private static JsonObject MergeSystemVariables(WorkflowDefinition definition, JsonObject? systemVariables)
    {
        var merged = new JsonObject();
        foreach (var variable in definition.Variables)
        {
            if (string.IsNullOrWhiteSpace(variable.Name))
            {
                continue;
            }

            merged[variable.Name] = variable.ResolveDefaultValue();
        }

        if (systemVariables != null)
        {
            foreach (var (name, value) in systemVariables)
            {
                merged[name] = value?.DeepClone();
            }
        }

        return merged;
    }

    /// <summary>
    /// 恢复挂起的流程实例（断点恢复）：从数据库加载节点状态，已完成节点不重跑，继续从待执行节点执行.
    /// </summary>
    public async Task<WorkflowInstance> ResumeAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        var instance = await _instanceStore.FindInstanceByIdAsync(instanceId, cancellationToken)
            ?? throw new WorkflowException($"流程实例 {instanceId} 不存在");

        if (instance.Status != InstanceStatus.Suspended && instance.Status != InstanceStatus.Created)
        {
            throw new WorkflowException($"流程实例 {instanceId} 当前状态为 {instance.Status}，只有挂起（Suspended）或未开始（Created）的实例可以恢复");
        }

        var definition = await _definitionStore.FindDefinitionByIdAsync(instance.DefinitionId, cancellationToken)
            ?? throw new WorkflowException($"工作流定义 {instance.DefinitionId} 不存在，无法恢复实例");

        if (definition.Version != instance.DefinitionVersion)
        {
            throw new WorkflowException($"工作流定义版本不匹配：实例引用 v{instance.DefinitionVersion}，当前定义为 v{definition.Version}");
        }

        var graph = _compiler.Compile(definition);
        return await ExecuteAsync(instance, graph, cancellationToken, resumed: true);
    }

    /// <summary>
    /// 取消执行中的实例.
    /// </summary>
    public async Task CancelAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        if (_runningInstances.TryGetValue(instanceId, out var cts))
        {
            await cts.CancelAsync();
            return;
        }

        // 不在运行中：直接落库为取消态（例如进程崩溃后）
        var instance = await _instanceStore.FindInstanceByIdAsync(instanceId, cancellationToken)
            ?? throw new WorkflowException($"流程实例 {instanceId} 不存在");
        instance.Status = InstanceStatus.Cancelled;
        instance.EndedAt = DateTimeOffset.Now;
        instance.UpdatedAt = DateTimeOffset.Now;
        await _instanceStore.SaveAsync(instance, cancellationToken);
        await _eventPublisher.PublishAsync(new WorkflowCancelledEvent { InstanceId = instanceId }, cancellationToken);
    }

    /// <summary>
    /// 查询实例.
    /// </summary>
    public Task<WorkflowInstance?> GetInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        return _instanceStore.FindInstanceByIdAsync(instanceId, cancellationToken);
    }

    /// <summary>
    /// 查询实例执行事件历史.
    /// </summary>
    public Task<IReadOnlyList<WorkflowEvent>> GetEventHistoryAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        return _eventLogStore.ListEventsAsync(instanceId, cancellationToken);
    }

    /// <summary>
    /// 执行或恢复实例.
    /// </summary>
    private async Task<WorkflowInstance> ExecuteAsync(
        WorkflowInstance instance,
        CompiledWorkflow graph,
        CancellationToken cancellationToken,
        bool resumed)
    {
        instance.Status = InstanceStatus.Running;
        instance.ErrorMessage = null;
        instance.StartedAt ??= DateTimeOffset.Now;
        instance.UpdatedAt = DateTimeOffset.Now;
        await _instanceStore.SaveAsync(instance, cancellationToken);

        if (resumed)
        {
            var completedCount = instance.NodeStates.Values.Count(n => n.State == NodeState.Completed);
            await _eventPublisher.PublishAsync(new WorkflowResumedEvent
            {
                InstanceId = instance.Id,
                CompletedNodeCount = completedCount,
            }, cancellationToken);
        }
        else
        {
            await _eventPublisher.PublishAsync(new WorkflowStartedEvent
            {
                InstanceId = instance.Id,
                DefinitionId = instance.DefinitionId,
            }, cancellationToken);
        }

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _runningInstances[instance.Id] = cts;
        try
        {
            instance = await _scheduler.RunAsync(instance, graph, cts.Token);
        }
        catch (OperationCanceledException)
        {
            instance.Status = InstanceStatus.Cancelled;
            instance.EndedAt = DateTimeOffset.Now;
            instance.UpdatedAt = DateTimeOffset.Now;
            await _instanceStore.SaveAsync(instance, CancellationToken.None);
            await _eventPublisher.PublishAsync(new WorkflowCancelledEvent { InstanceId = instance.Id }, CancellationToken.None);
        }
        finally
        {
            _runningInstances.TryRemove(instance.Id, out _);
            cts.Dispose();
        }

        return instance;
    }
}
