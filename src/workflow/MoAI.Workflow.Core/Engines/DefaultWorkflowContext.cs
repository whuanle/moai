using MoAI.Workflow.Models;

namespace MoAI.Workflow.Engines;

/// <summary>
/// 流程执行上下文.
/// </summary>
public class DefaultWorkflowContext : IWorkflowContext
{
    private readonly List<IWorkflowNodeRunResult> _runResults = new List<IWorkflowNodeRunResult>();

    /// <summary>
    /// 实例.
    /// </summary>
    public Guid InstanceId { get; init; } = Guid.CreateVersion7();

    /// <summary>
    /// 流程定义的 id.
    /// </summary>
    public Guid DefinitionId { get; init; } = Guid.CreateVersion7();

    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; init; } = Guid.CreateVersion7();

    /// <summary>
    /// 发起请求的用户 id.
    /// </summary>
    public string UserId { get; init; } = default!;

    /// <summary>
    /// 运行状态.
    /// </summary>
    public WorkflowRunStatus Status { get; set; } = WorkflowRunStatus.Running;

    /// <summary>
    /// 启动时间.
    /// </summary>
    public DateTimeOffset StartTime { get; init; } = DateTimeOffset.Now;

    /// <summary>
    /// 系统参数.
    /// </summary>
    public Dictionary<string, object> SystemVariables { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// 全局参数，启动时默认没有，需要用户自己配置执行流程时自动赋值.
    /// </summary>
    public Dictionary<string, object> GlobalVariables { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// 系统参数.
    /// </summary>
    public string SystemVariablesJson { get; init; } = "{}";

    /// <summary>
    /// 系统参数.
    /// </summary>
    public string GlobalVariablesJson { get; set; } = "{}";

    /// <inheritdoc />
    public IReadOnlyCollection<IWorkflowNodeRunResult> NodeRunResults => _runResults;

    /// <summary>
    /// 服务容器.
    /// </summary>
    public IServiceProvider ServiceProvider { get; init; } = default!;

    /// <inheritdoc />
    public IWorkflowNodeRunResult CurrntNode { get; set; } = default!;

    /// <inheritdoc/>
    public event NodeRunResultChangedEventHandler NodeRunResultChanged = default!;

    /// <inheritdoc/>
    public event WorkflowRunStatusChangedEventHandler WorkflowRunStatusChanged = default!;

    /// <summary>
    /// 插入正在运行的节点到上下文.
    /// </summary>
    /// <param name="nodeRunResult"></param>
    public void AddRunningNode(IWorkflowNodeRunResult nodeRunResult)
    {
        _runResults.Add(nodeRunResult);
    }

    public async Task OnNodeRunResultChangedAsync(IWorkflowNodeRunResult nodeRunResult)
    {
        if (NodeRunResultChanged != null)
        {
            await NodeRunResultChanged(this, nodeRunResult);
        }
    }

    public async Task OnWorkflowRunStatusChangedAsync(WorkflowRunStatus workflowRunStatus)
    {
        if (WorkflowRunStatusChanged != null)
        {
            await WorkflowRunStatusChanged(this, workflowRunStatus);
        }
    }
}
