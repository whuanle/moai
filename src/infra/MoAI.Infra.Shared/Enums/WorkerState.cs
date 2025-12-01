namespace MoAI.Wiki.Models;

/// <summary>
/// 任务通用状态.
/// </summary>
public enum WorkerState
{
    /// <summary>
    /// 无状态.
    /// </summary>
    None = 0,

    /// <summary>
    /// 等待处理.
    /// </summary>
    Wait,

    /// <summary>
    /// 正在处理.
    /// </summary>
    Processing,

    /// <summary>
    /// 取消.
    /// </summary>
    Cancal,

    /// <summary>
    /// 成功.
    /// </summary>
    Successful,

    /// <summary>
    /// 失败.
    /// </summary>
    Failed
}