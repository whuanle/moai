using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Plugin.Commands;

/// <summary>
/// 删除插件.
/// </summary>
public class DeletePluginCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 插件.
    /// </summary>
    public int PluginId { get; init; }
}