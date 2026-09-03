using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 动态示例插件配置.
/// </summary>
public class DynamicGreetConfig
{
    /// <summary>
    /// 问候前缀.
    /// </summary>
    [Description("问候前缀")]
    public string Prefix { get; set; } = string.Empty;
}
