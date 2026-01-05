using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Plugin.Authorization.Commands;

/// <summary>
/// 批量撤销某个团队的插件授权.
/// </summary>
public class BatchRevokePluginsFromTeamCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 团队ID.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 要撤销的插件ID列表.
    /// </summary>
    public IReadOnlyCollection<int> PluginIds { get; init; } = new List<int>();
}
