using System.Collections.Generic;

namespace MoAI.TeamPlugin.Queries.Responses;

/// <summary>
/// 团队可用插件列表查询响应.
/// </summary>
public class QueryTeamPluginsCommandResponse
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 我在团队中的角色：0=Owner 1=Admin 2=Member.
    /// </summary>
    public int MyRole { get; init; }

    /// <summary>
    /// 可管理（Owner/Admin）.
    /// </summary>
    public bool CanManage { get; init; }

    /// <summary>
    /// 团队可用插件列表（团队自有插件 + 可用的系统插件）.
    /// </summary>
    public List<TeamPluginItem> Items { get; init; } = new();
}
