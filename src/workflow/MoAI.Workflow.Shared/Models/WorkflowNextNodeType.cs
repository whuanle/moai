namespace MoAI.Workflow.Models;

public enum WorkflowNextNodeType
{
    /// <summary>
    /// 没有设置链接.
    /// </summary>
    None,

    /// <summary>
    /// 条件.
    /// </summary>
    Condition,

    /// <summary>
    /// 并发.
    /// </summary>
    Parallel,

    /// <summary>
    /// 顺序.
    /// </summary>
    Order,

    /// <summary>
    /// 迭代.
    /// </summary>
    Intera
}