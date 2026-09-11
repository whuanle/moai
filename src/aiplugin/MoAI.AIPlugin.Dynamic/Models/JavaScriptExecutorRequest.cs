using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// JavaScript 执行器动态插件请求参数.
/// </summary>
public class JavaScriptExecutorRequest
{
    /// <summary>
    /// 传给 <c>run(parameter)</c> 的字符串参数（通常填 JSON 文本，由 JS 端自行 <c>JSON.parse</c>）.
    /// </summary>
    [Description("传给 run(parameter) 的字符串参数；通常填 JSON 文本，JS 端可自行 JSON.parse")]
    public string Parameters { get; set; } = string.Empty;
}