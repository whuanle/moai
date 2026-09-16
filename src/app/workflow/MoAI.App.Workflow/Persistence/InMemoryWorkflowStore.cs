using System.Text.Json;
using MoAI.App.Workflow.Definition;
using MoAI.App.Workflow.Events;

namespace MoAI.App.Workflow.Persistence;

/// <summary>
/// 内存存储实现 - 用于单元测试或无数据库场景，接口与 SQLite 实现完全一致.
/// </summary>
public class InMemoryWorkflowStore : IWorkflowDefinitionStore, IWorkflowInstanceStore, IWorkflowEventLogStore
{
    private readonly object _lock = new();
    private readonly Dictionary<string, WorkflowDefinition> _definitions = new();
    private readonly Dictionary<string, Instance.WorkflowInstance> _instances = new();
    private readonly List<WorkflowEvent> _events = new();

    /// <inheritdoc/>
    public Task SaveDefinitionAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _definitions[definition.Id] = CloneDefinition(definition);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<WorkflowDefinition?> FindDefinitionByIdAsync(string definitionId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_definitions.TryGetValue(definitionId, out var definition)
                ? CloneDefinition(definition)
                : null);
        }
    }

    /// <inheritdoc/>
    public Task<WorkflowDefinition?> FindPublishedDefinitionByIdAsync(string definitionId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            WorkflowDefinition? result = null;
            if (_definitions.TryGetValue(definitionId, out var definition) && definition.Status == DefinitionStatus.Published)
            {
                result = CloneDefinition(definition);
            }

            return Task.FromResult(result);
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<WorkflowDefinition>> ListDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            IReadOnlyList<WorkflowDefinition> result = _definitions.Values
                .OrderByDescending(d => d.Version)
                .Select(CloneDefinition)
                .ToList();
            return Task.FromResult(result);
        }
    }

    /// <inheritdoc/>
    public Task PublishAsync(string definitionId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_definitions.TryGetValue(definitionId, out var definition))
            {
                definition.Status = DefinitionStatus.Published;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task CreateAsync(Instance.WorkflowInstance instance, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _instances[instance.Id] = CloneInstance(instance);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SaveAsync(Instance.WorkflowInstance instance, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _instances[instance.Id] = CloneInstance(instance);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<Instance.WorkflowInstance?> FindInstanceByIdAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_instances.TryGetValue(instanceId, out var instance) ? CloneInstance(instance) : null);
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<Instance.WorkflowInstance>> ListInstancesAsync(string? definitionId = null, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            IReadOnlyList<Instance.WorkflowInstance> result = _instances.Values
                .Where(i => definitionId == null || i.DefinitionId == definitionId)
                .OrderByDescending(i => i.CreatedAt)
                .Select(CloneInstance)
                .ToList();
            return Task.FromResult(result);
        }
    }

    /// <inheritdoc/>
    public Task AppendAsync(WorkflowEvent workflowEvent, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _events.Add(workflowEvent);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<WorkflowEvent>> ListEventsAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            IReadOnlyList<WorkflowEvent> result = _events
                .Where(e => e.InstanceId == instanceId)
                .ToList();
            return Task.FromResult(result);
        }
    }

    private static WorkflowDefinition CloneDefinition(WorkflowDefinition definition)
    {
        return WorkflowJson.DeserializeDefinition(WorkflowJson.SerializeDefinition(definition));
    }

    private static Instance.WorkflowInstance CloneInstance(Instance.WorkflowInstance instance)
    {
        var json = JsonSerializer.Serialize(instance, WorkflowJson.Options);
        return JsonSerializer.Deserialize<Instance.WorkflowInstance>(json, WorkflowJson.Options)!;
    }
}
