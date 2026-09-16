using MoAI.App.Workflow.Definition;

namespace MoAI.App.Workflow.Persistence;

/// <summary>
/// 工作流定义存储接口 - 流程定义的持久化（保存草稿/发布/查询）.
/// 迁移到正式项目时对接数据库表（对应旧版 AppWorkflowDesigns）.
/// </summary>
public interface IWorkflowDefinitionStore
{
    /// <summary>
    /// 保存工作流定义（新增或覆盖）.
    /// </summary>
    Task SaveDefinitionAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按 ID 查询定义（任意状态，取最新版本）.
    /// </summary>
    Task<WorkflowDefinition?> FindDefinitionByIdAsync(string definitionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按 ID 查询已发布的定义，用于创建流程实例.
    /// </summary>
    Task<WorkflowDefinition?> FindPublishedDefinitionByIdAsync(string definitionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询全部定义.
    /// </summary>
    Task<IReadOnlyList<WorkflowDefinition>> ListDefinitionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 发布定义（Draft → Published），发布后定义不可变.
    /// </summary>
    Task PublishAsync(string definitionId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 工作流实例存储接口 - 流程实例与节点级状态的持久化，支撑断点恢复.
/// </summary>
public interface IWorkflowInstanceStore
{
    /// <summary>
    /// 新增实例.
    /// </summary>
    Task CreateAsync(Instance.WorkflowInstance instance, CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存实例全量状态（检查点：每个节点状态变更后调用）.
    /// </summary>
    Task SaveAsync(Instance.WorkflowInstance instance, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按实例 ID 查询（含全部节点状态）.
    /// </summary>
    Task<Instance.WorkflowInstance?> FindInstanceByIdAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询实例列表.
    /// </summary>
    /// <param name="definitionId">按定义过滤（可为空）.</param>
    Task<IReadOnlyList<Instance.WorkflowInstance>> ListInstancesAsync(string? definitionId = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// 工作流事件日志接口 - 执行事件的持久化，实例恢复后仍可追溯执行历史.
/// </summary>
public interface IWorkflowEventLogStore
{
    /// <summary>
    /// 追加一条事件.
    /// </summary>
    Task AppendAsync(Events.WorkflowEvent workflowEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询实例的全部事件（按时间顺序）.
    /// </summary>
    Task<IReadOnlyList<Events.WorkflowEvent>> ListEventsAsync(string instanceId, CancellationToken cancellationToken = default);
}
