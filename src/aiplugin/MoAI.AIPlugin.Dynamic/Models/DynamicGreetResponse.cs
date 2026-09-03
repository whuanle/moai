using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 动态示例插件响应结果.
/// </summary>
public class DynamicGreetResponse
{
    /// <summary>
    /// 问候语.
    /// </summary>
    [Description("问候语")]
    public string Message { get; set; } = string.Empty;
}
