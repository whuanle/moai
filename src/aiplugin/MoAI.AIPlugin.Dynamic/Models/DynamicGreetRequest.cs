using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 动态示例插件请求参数.
/// </summary>
public class DynamicGreetRequest
{
    /// <summary>
    /// 对方名字.
    /// </summary>
    [Description("对方名字")]
    public string Name { get; set; } = string.Empty;
}
