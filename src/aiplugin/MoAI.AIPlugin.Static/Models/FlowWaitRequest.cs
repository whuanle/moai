using System.ComponentModel;

namespace MoAI.AIPlugin.Static.Models;

/// <summary>
/// 流程等待请求参数.
/// </summary>
public class FlowWaitRequest
{
    /// <summary>
    /// 等待时间（秒）.
    /// </summary>
    [Description("等待时间（秒），不能为负数")]
    public int WaitTimeInSeconds { get; set; }
}
