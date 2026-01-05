using MediatR;
using MoAI.Plugin.TeamPlugins.Queries.Responses;

namespace MoAI.Plugin.TeamPlugins.Queries;

/// <summary>
/// 查询团队可用的插件列表（包含公开插件和团队专属插件）.
/// </summary>
public class QueryTeamAvailablePluginListCommand : IRequest<QueryTeamAvailablePluginListCommandResponse>
{
    /// <summary>
    /// 团队 ID.
    /// </summary>
    public int TeamId { get; init; }
}
