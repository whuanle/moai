using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Plugin.NativePlugins.Commands;

/// <summary>
/// 删除内置插件实例.
/// </summary>
public class DeleteNativePluginCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 插件 id.
    /// </summary>
    public int PluginId { get; init; }
}