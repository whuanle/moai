using System.Text.Json.Nodes;
using MoAI.App.Workflow.Definition;

namespace MoAI.App.Workflow.Instance;

/// <summary>
/// 流程实例 - 一次工作流执行的完整状态快照.
/// 节点级状态会被持久化到数据库，是"断点恢复"（流程恢复）的依据.
/// </summary>
public class WorkflowInstance
{
    /// <summary>
    /// 实例唯一标识符.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 工作流定义 ID.
    /// </summary>
    public string DefinitionId { get; set; } = string.Empty;

    /// <summary>
    /// 工作流名称（冗余存储，便于查询展示）.
    /// </summary>
    public string DefinitionName { get; set; } = string.Empty;

    /// <summary>
    /// 执行时引用的工作流定义版本.
    /// </summary>
    public int DefinitionVersion { get; set; } = 1;

    /// <summary>
    /// 实例状态.
    /// </summary>
    public InstanceStatus Status { get; set; } = InstanceStatus.Created;

    /// <summary>
    /// 工作流启动参数.
    /// </summary>
    public JsonObject Input { get; set; } = new();

    /// <summary>
    /// 全局变量实际值（启动时默认值与传入值合并的结果），节点通过 system.变量名 引用；
    /// 随实例持久化，断点恢复后作用域仍可重建.
    /// </summary>
    public JsonObject SystemVariables { get; set; } = new();

    /// <summary>
    /// 调用方注入的系统上下文（sys.* 附加变量），随实例持久化，断点恢复后作用域仍可重建.
    /// 对话式调用（发布应用会话）注入 userId/appId/conversationId/messageId/history；
    /// 未注入时 sys.* 仅有引擎内置项.
    /// </summary>
    public JsonObject? SystemContext { get; set; }

    /// <summary>
    /// 工作流最终输出（结束节点收集）.
    /// </summary>
    public JsonObject? Output { get; set; }

    /// <summary>
    /// 失败/挂起原因.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 节点执行状态表，键为节点 Key.
    /// </summary>
    public Dictionary<string, NodeExecutionState> NodeStates { get; set; } = new();

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>
    /// 开始执行时间.
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// 结束时间（完成/取消）.
    /// </summary>
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>
    /// 最后更新时间（每次检查点刷新）.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}

/// <summary>
/// 节点执行状态 - 实例中单个节点的执行记录.
/// </summary>
public class NodeExecutionState
{
    /// <summary>
    /// 节点 Key.
    /// </summary>
    public string NodeKey { get; set; } = string.Empty;

    /// <summary>
    /// 节点类型.
    /// </summary>
    public string NodeType { get; set; } = string.Empty;

    /// <summary>
    /// 节点名称.
    /// </summary>
    public string NodeName { get; set; } = string.Empty;

    /// <summary>
    /// 节点状态.
    /// </summary>
    public NodeState State { get; set; } = NodeState.Pending;

    /// <summary>
    /// 已解析的节点输入（检查点持久化，恢复时可追溯）.
    /// </summary>
    public JsonObject? Input { get; set; }

    /// <summary>
    /// 节点输出.
    /// </summary>
    public JsonObject? Output { get; set; }

    /// <summary>
    /// 错误信息.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 执行尝试次数（恢复重跑会累加）.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// 最近一次开始时间.
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// 最近一次结束时间.
    /// </summary>
    public DateTimeOffset? EndedAt { get; set; }
}
