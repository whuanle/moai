namespace MoAI.App.Workflow.Definition;

/// <summary>
/// 节点执行状态枚举.
/// </summary>
public enum NodeState
{
    /// <summary>
    /// 待执行.
    /// </summary>
    Pending,

    /// <summary>
    /// 执行中.
    /// </summary>
    Running,

    /// <summary>
    /// 已完成.
    /// </summary>
    Completed,

    /// <summary>
    /// 失败.
    /// </summary>
    Failed,

    /// <summary>
    /// 已跳过（条件分支未命中，或上游全部被跳过）.
    /// </summary>
    Skipped,
}
