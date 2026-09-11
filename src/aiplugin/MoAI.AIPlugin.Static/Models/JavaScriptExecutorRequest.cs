using System.ComponentModel;

namespace MoAI.AIPlugin.Static.Models;

/// <summary>
/// JavaScript 执行器请求参数.
/// </summary>
public class JavaScriptExecutorRequest
{
    /// <summary>
    /// 要执行的 JavaScript 代码，必须定义一个无参 run() 函数并把执行结果 return 出来.
    /// </summary>
    [Description("要执行的 JavaScript 代码（必须定义一个无参 run() 函数，把结果 return 出来）")]
    public string Code { get; set; } = string.Empty;
}