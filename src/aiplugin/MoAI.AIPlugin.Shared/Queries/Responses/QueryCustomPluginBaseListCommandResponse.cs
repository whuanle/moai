using System.Collections.Generic;

namespace MoAI.AIPlugin.Queries.Responses;

/// <summary>
/// 自定义插件基础信息列表响应.
/// </summary>
public class QueryCustomPluginBaseListCommandResponse
{
    /// <summary>
    /// 列表.
    /// </summary>
    public IReadOnlyCollection<PluginBaseInfoItem> Items { get; init; } = new List<PluginBaseInfoItem>();
}
