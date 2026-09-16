namespace MoAI.App.Workflow.Queries.Responses;

/// <summary>
/// 流程应用运行实例详情响应.
/// </summary>
public class QueryAppWorkflowInstanceCommandResponse
{
    /// <summary>
    /// 实例 id.
    /// </summary>
    public Guid InstanceId { get; set; }

    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; set; }

    /// <summary>
    /// 实例状态，0=已创建 1=执行中 2=已挂起 3=已完成 4=已取消.
    /// </summary>
    public short Status { get; set; }

    /// <summary>
    /// 是否调试运行.
    /// </summary>
    public bool IsDebug { get; set; }

    /// <summary>
    /// 执行引用的定义版本号，0=调试执行.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// 启动参数 JSON 文本.
    /// </summary>
    public string Input { get; set; } = string.Empty;

    /// <summary>
    /// 最终输出 JSON 文本，未产出为 null.
    /// </summary>
    public string? Output { get; set; }

    /// <summary>
    /// 失败/挂起原因，无异常为 null.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 开始执行时间.
    /// </summary>
    public DateTimeOffset? StartTime { get; set; }

    /// <summary>
    /// 结束时间.
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>
    /// 各节点执行状态.
    /// </summary>
    public IReadOnlyList<WorkflowNodeExecution> Nodes { get; set; } = new List<WorkflowNodeExecution>();
}
