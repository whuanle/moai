using MediatR;
using MoAI.Infra.Models;
using MoAI.Plugin.Models;

namespace MoAI.Plugin.CustomPlugins.Commands;

/// <summary>
/// 更新 MCP 插件.
/// </summary>
public class UpdateMcpServerPluginCommand : McpServerPluginConnectionOptions, IRequest<EmptyCommandResponse>
{
    /// <summary>
    ///  插件 id.
    /// </summary>
    public int PluginId { get; init; }

    /// <summary>
    /// 分类 id.
    /// </summary>
    public int ClassifyId { get; init; }

    /// <summary>
    /// 是否公开.
    /// </summary>
    public bool IsPublic { get; init; } = default!;
}