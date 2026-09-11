using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// JavaScript 执行器动态插件配置.
/// </summary>
/// <remarks>
/// 用户在实例配置中粘贴一段 JavaScript 代码，代码必须导出 <c>run(parameter)</c> 函数：
/// <c>parameter</c> 为字符串参数（通常由 <see cref="JavaScriptExecutorRequest.Parameters"/> 透传），
/// 函数返回值（任意 JS 类型）会被序列化进 <see cref="JavaScriptExecutorResponse.ResultJson"/>.
/// </remarks>
public class JavaScriptExecutorConfig
{
    /// <summary>
    /// 要执行的 JavaScript 代码.
    /// </summary>
    [Description("JavaScript 代码：必须定义 function run(parameter) {...}；parameter 是字符串，返回值会被序列化返回")]
    public string JavaScriptCode { get; set; } = string.Empty;
}