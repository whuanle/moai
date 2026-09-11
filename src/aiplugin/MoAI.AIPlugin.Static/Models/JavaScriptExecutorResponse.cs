using System.ComponentModel;

namespace MoAI.AIPlugin.Static.Models;

/// <summary>
/// JavaScript 执行器响应结果.
/// </summary>
public class JavaScriptExecutorResponse
{
    /// <summary>
    /// JavaScript 执行结果（任意 JSON 可序列化对象）.
    /// </summary>
    [Description("JavaScript 执行结果（任意可序列化为 JSON 的对象；run() 未返回或返回 null/undefined 时为 null）")]
    public object? Result { get; set; }
}