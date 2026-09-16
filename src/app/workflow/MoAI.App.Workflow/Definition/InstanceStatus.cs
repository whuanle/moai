namespace MoAI.App.Workflow.Definition;

/// <summary>
/// 流程实例状态枚举.
/// </summary>
public enum InstanceStatus
{
    /// <summary>
    /// 已创建，尚未开始执行.
    /// </summary>
    Created,

    /// <summary>
    /// 执行中.
    /// </summary>
    Running,

    /// <summary>
    /// 已挂起 - 节点执行失败或被手动挂起，可从断点恢复.
    /// </summary>
    Suspended,

    /// <summary>
    /// 已完成.
    /// </summary>
    Completed,

    /// <summary>
    /// 已取消.
    /// </summary>
    Cancelled,
}
