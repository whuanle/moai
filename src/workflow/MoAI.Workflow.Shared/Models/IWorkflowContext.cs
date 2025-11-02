using MoAI.Workflow.Models;

namespace MoAI.Workflow.Engines;

public delegate Task NodeRunResultChangedEventHandler(IWorkflowContext context, IWorkflowNodeRunResult nodeRunResult);

public delegate Task WorkflowRunStatusChangedEventHandler(IWorkflowContext context, WorkflowRunStatus workflowRunStatus);

/// <summary>
/// 流程执行上下文.
/// </summary>
public interface IWorkflowContext
{
    /// <summary>
    /// 实例.
    /// </summary>
    Guid InstanceId { get; }

    /// <summary>
    /// 流程定义的 id.
    /// </summary>
    Guid DefinitionId { get; }

    /// <summary>
    /// 应用 id.
    /// </summary>
    Guid AppId { get; }

    /// <summary>
    /// 发起请求的用户 id.
    /// </summary>
    string UserId { get; }

    /// <summary>
    /// 运行状态.
    /// </summary>
    WorkflowRunStatus Status { get; set; }

    /// <summary>
    /// 启动时间.
    /// </summary>
    DateTimeOffset StartTime { get; }

    /// <summary>
    /// 系统参数.
    /// </summary>
    Dictionary<string, object> SystemVariables { get; }

    /// <summary>
    /// 全局参数，启动时默认没有，需要用户自己配置执行流程时自动赋值.
    /// </summary>
    Dictionary<string, object> GlobalVariables { get; }

    /// <summary>
    /// 系统参数.
    /// </summary>
    string SystemVariablesJson { get; }

    /// <summary>
    /// 系统参数.
    /// </summary>
    string GlobalVariablesJson { get; set; }

    /// <summary>
    /// 已被执行的流程.
    /// </summary>
    IReadOnlyCollection<IWorkflowNodeRunResult> NodeRunResults { get; }

    /// <summary>
    /// 当前正在执行的节点.
    /// </summary>
    IWorkflowNodeRunResult CurrntNode { get; }

    /// <summary>
    /// 节点执行事件.
    /// </summary>
    public event NodeRunResultChangedEventHandler NodeRunResultChanged;

    /// <summary>
    /// 流程状态变化事件，完成、取消、失败等.
    /// </summary>
    public event WorkflowRunStatusChangedEventHandler WorkflowRunStatusChanged;
}