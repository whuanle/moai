using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Plugin.CustomPlugins.Commands;

/// <summary>
/// 刷新 MCP 服务器的工具列表.
/// </summary>
public class RefreshMcpServerPluginCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    ///  插件 id.
    /// </summary>
    public int PluginId { get; init; }
}
