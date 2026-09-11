using System.ComponentModel;

namespace MoAI.AIPlugin.Static.Models;

/// <summary>
/// 获取当前时间响应结果.
/// </summary>
public class CurrentTimeResponse
{
    /// <summary>
    /// 当前系统时间.
    /// </summary>
    [Description("当前系统时间")]
    public string CurrentTime { get; set; } = string.Empty;
}
