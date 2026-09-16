using System.Text.Json.Serialization;
using MoAI.Infra.Models;

namespace MoAI.App.Workflow.Queries.Responses;

/// <summary>
/// 节点执行状态（调试/详情响应共用）.
/// </summary>
public class WorkflowNodeExecution
{
    /// <summary>
    /// 节点 Key.
    /// </summary>
    public string NodeKey { get; set; } = string.Empty;

    /// <summary>
    /// 节点类型（start/end/condition/aiChat/javascript/plugin）.
    /// </summary>
    public string NodeType { get; set; } = string.Empty;

    /// <summary>
    /// 节点名称.
    /// </summary>
    public string NodeName { get; set; } = string.Empty;

    /// <summary>
    /// 执行状态：pending/running/completed/failed/skipped.
    /// </summary>
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// 解析后的节点输入 JSON 文本，未执行为 null.
    /// </summary>
    public string? Input { get; set; }

    /// <summary>
    /// 节点输出 JSON 文本，未执行为 null.
    /// </summary>
    public string? Output { get; set; }

    /// <summary>
    /// 失败原因，未失败为 null.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 执行次数（失败重试/恢复后重跑会累加）.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// 开始执行时间.
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// 结束执行时间.
    /// </summary>
    public DateTimeOffset? EndedAt { get; set; }
}

/// <summary>
/// 调试执行响应：一次同步执行的终态快照.
/// </summary>
public class DebugRunAppWorkflowResponse
{
    /// <summary>
    /// 实例 id.
    /// </summary>
    public Guid InstanceId { get; set; }

    /// <summary>
    /// 实例状态：created/running/suspended/completed/cancelled.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 执行引用的定义版本，调试运行为 0.
    /// </summary>
    public int DefinitionVersion { get; set; }

    /// <summary>
    /// 工作流最终输出 JSON 文本，未产出为 null.
    /// </summary>
    public string? Output { get; set; }

    /// <summary>
    /// 失败/挂起原因，无异常为 null.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 开始执行时间.
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// 结束时间.
    /// </summary>
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>
    /// 各节点执行状态.
    /// </summary>
    public IReadOnlyList<WorkflowNodeExecution> Nodes { get; set; } = new List<WorkflowNodeExecution>();
}
