using System.ComponentModel;

namespace MoAI.AIPlugin.Static.Models;

/// <summary>
/// 流程等待响应结果.
/// </summary>
public class FlowWaitResponse
{
    /// <summary>
    /// 等待结果说明.
    /// </summary>
    [Description("等待结果说明")]
    public string Message { get; set; } = string.Empty;
}
