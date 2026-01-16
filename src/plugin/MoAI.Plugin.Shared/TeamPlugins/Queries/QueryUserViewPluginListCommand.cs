using MediatR;
using MoAI.Plugin.TeamPlugins.Queries.Responses;

namespace MoAI.Plugin.TeamPlugins.Queries;

/// <summary>
/// 查询用户可见的插件列表（团队下可用的插件）.
/// </summary>
public class QueryUserViewPluginListCommand : IRequest<QueryUserViewPluginListCommandResponse>
{
    /// <summary>
    /// 团队 ID.
    /// </summary>
    public int TeamId { get; init; }
}
