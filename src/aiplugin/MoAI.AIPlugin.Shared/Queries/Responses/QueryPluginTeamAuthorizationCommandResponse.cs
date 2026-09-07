using System;
using System.Collections.Generic;

namespace MoAI.AIPlugin.Queries.Responses;

/// <summary>
/// 私有系统插件团队授权查询响应.
/// </summary>
public class QueryPluginTeamAuthorizationCommandResponse
{
    /// <summary>
    /// 系统插件记录 id.
    /// </summary>
    public Guid PluginId { get; init; }

    /// <summary>
    /// 插件是否公开；公开成员对所有团队可用，无需授权.
    /// </summary>
    public bool IsPublic { get; init; }

    /// <summary>
    /// 已授权团队列表（公开插件为空）.
    /// </summary>
    public List<QueryPluginTeamAuthorizationCommandResponseItem> Items { get; init; } = new();
}
