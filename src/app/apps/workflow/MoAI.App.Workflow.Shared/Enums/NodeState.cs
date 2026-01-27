namespace MoAI.Workflow.Enums;

/// <summary>
/// 节点执行状态枚举.
/// </summary>
public enum NodeState
{
    /// <summary>
    /// 待执行 - 节点尚未开始执行.
    /// </summary>
    Pending,

    /// <summary>
    /// 执行中 - 节点正在执行.
    /// </summary>
    Running,

    /// <summary>
    /// 已完成 - 节点成功完成执行.
    /// </summary>
    Completed,

    /// <summary>
    /// 失败 - 节点执行失败.
    /// </summary>
    Failed,
}
