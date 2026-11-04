using System;
using System.Reflection;

namespace MoAI.AIPlugin.Models;

/// <summary>
/// 多工具插件中的单个工具元数据（由执行引擎扫描 <see cref="Attributes.AiPluginToolAttribute"/> 标记的方法生成）.
/// </summary>
public class PluginToolInfo
{
    /// <summary>
    /// 工具唯一标识（插件内唯一）.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// 工具展示标题.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 工具描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 请求参数类型.
    /// </summary>
    public Type Request { get; init; } = null!;

    /// <summary>
    /// 响应结果类型.
    /// </summary>
    public Type Response { get; init; } = null!;

    /// <summary>
    /// 执行方法（Task&lt;TResponse&gt; XxxAsync(TRequest, CancellationToken)）.
    /// </summary>
    public MethodInfo Method { get; init; } = null!;

    /// <summary>
    /// 参数运行示例 JSON 字符串，可为空.
    /// </summary>
    public string? ParamsExample { get; init; }
}
