namespace MoAI.Infra.Models;

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
    Wait = 1,

    /// <summary>
    /// 正在处理.
    /// </summary>
    Processing = 2,

    /// <summary>
    /// 取消.
    /// </summary>
    Cancal = 3,

    /// <summary>
    /// 成功.
    /// </summary>
    Successful = 4,

    /// <summary>
    /// 失败.
    /// </summary>
    Failed = 5
}
