using System.Collections.Generic;

namespace MoAI.AIPlugin.Queries.Responses;

/// <summary>
/// 插件函数列表响应.
/// </summary>
public class QueryCustomPluginFunctionsListCommandResponse
{
    /// <summary>
    /// 函数列表.
    /// </summary>
    public IReadOnlyCollection<PluginFunctionItem> Items { get; init; } = new List<PluginFunctionItem>();
}
