using System.Collections.Generic;

namespace MoAI.AiPlugin.Queries.Responses;

/// <summary>
/// 插件列表查询响应.
/// </summary>
public class QueryPluginListCommandResponse
{
    /// <summary>
    /// 插件列表.
    /// </summary>
    public List<QueryPluginListCommandResponseItem> Items { get; init; } = new();
}
