using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Plugin.TeamPlugins.Commands;

/// <summary>
/// 检测插件是否属于指定团队.
/// </summary>
public class CheckPluginBelongsToTeamCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 插件 id.
    /// </summary>
    public int PluginId { get; set; }

    /// <summary>
    /// 团队 id.
    /// </summary>
    public int TeamId { get; set; }
}
