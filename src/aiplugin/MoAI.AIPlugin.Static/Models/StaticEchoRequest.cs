using System.ComponentModel;

namespace MoAI.AIPlugin.Static.Models;

/// <summary>
/// 静态示例插件请求参数.
/// </summary>
public class StaticEchoRequest
{
    /// <summary>
    /// 待回显的消息.
    /// </summary>
    [Description("待回显的消息")]
    public string Message { get; set; } = string.Empty;
}
