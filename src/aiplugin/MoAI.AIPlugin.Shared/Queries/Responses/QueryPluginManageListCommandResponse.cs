using System.Collections.Generic;

namespace MoAI.AIPlugin.Queries.Responses;

/// <summary>
/// 插件管理列表查询响应.
/// </summary>
public class QueryPluginManageListCommandResponse
{
    /// <summary>
    /// 插件列表.
    /// </summary>
    public List<QueryPluginManageListCommandResponseItem> Items { get; init; } = new();
}
