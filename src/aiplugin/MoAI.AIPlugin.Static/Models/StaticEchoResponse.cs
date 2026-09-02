using System.ComponentModel;

namespace MoAI.AIPlugin.Static.Models;

/// <summary>
/// 静态示例插件响应结果.
/// </summary>
public class StaticEchoResponse
{
    /// <summary>
    /// 回显结果.
    /// </summary>
    [Description("回显结果")]
    public string Message { get; set; } = string.Empty;
}
