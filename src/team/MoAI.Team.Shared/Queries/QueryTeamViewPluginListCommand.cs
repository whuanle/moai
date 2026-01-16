using MediatR;
using MoAI.Team.Queries.Responses;

namespace MoAI.Team.Queries;

/// <summary>
/// 按团队角度查询可用的插件列表（包含公开插件、团队专属插件和团队被授权的插件）.
/// </summary>
public class QueryTeamViewPluginListCommand : IRequest<QueryTeamViewPluginListCommandResponse>
{
    /// <summary>
    /// 团队id.
    /// </summary>
    public int TeamId { get; set; }
}
