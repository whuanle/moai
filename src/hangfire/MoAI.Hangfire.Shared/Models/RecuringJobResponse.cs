namespace MoAI.Hangfire.Models;

/// <summary>
/// 执行定时任务结果.
/// </summary>
public class RecuringJobResponse
{
    /// <summary>
    /// 是否取消后续任务.
    /// </summary>
    public bool IsCancel { get; set; }
}