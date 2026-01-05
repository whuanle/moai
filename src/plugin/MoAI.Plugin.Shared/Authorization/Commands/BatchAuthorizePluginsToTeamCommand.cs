using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Plugin.Authorization.Commands;

/// <summary>
/// 批量授权插件给某个团队.
/// </summary>
public class BatchAuthorizePluginsToTeamCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 团队ID.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 要授权的插件ID列表.
    /// </summary>
    public IReadOnlyCollection<int> PluginIds { get; init; } = new List<int>();
}
