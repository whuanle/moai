using System;

namespace MoAI.AIPlugin.Queries.Responses;

/// <summary>
/// 插件函数项.
/// </summary>
public class PluginFunctionItem
{
    /// <summary>
    /// 函数 id.
    /// </summary>
    public Guid FunctionId { get; set; }

    /// <summary>
    /// 插件自定义 id.
    /// </summary>
    public Guid PluginId { get; set; }

    /// <summary>
    /// 函数名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Summary { get; set; } = default!;

    /// <summary>
    /// api 路径.
    /// </summary>
    public string Path { get; set; } = default!;
}
