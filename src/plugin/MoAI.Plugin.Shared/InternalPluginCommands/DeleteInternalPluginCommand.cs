using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Plugin.BuiltCommands;


/// <summary>
/// 删除内置插件实例.
/// </summary>
public class DeleteInternalPluginCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 插件 id.
    /// </summary>
    public int PluginId { get; init; }
}