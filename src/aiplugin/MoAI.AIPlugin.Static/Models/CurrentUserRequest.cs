using System.ComponentModel;

namespace MoAI.AIPlugin.Static.Models;

/// <summary>
/// 获取当前用户信息的请求参数（无参数，用户信息来自执行上下文注入）.
/// </summary>
public class CurrentUserRequest
{
    /// <summary>
    /// 预留参数，当前无可用参数.
    /// </summary>
    [Description("预留参数，当前无可用参数")]
    public string? Placeholder { get; set; }
}
