using MediatR;
using MoAI.Infra.Models;
using MoAI.Plugin.Models;

namespace MoAI.Plugin.CustomPlugins.Commands;

/// <summary>
/// 导入 mcp 服务.
/// </summary>
public class ImportMcpServerPluginCommand : McpServerPluginConnectionOptions, IRequest<SimpleInt>
{
    /// <summary>
    /// 分类 id.
    /// </summary>
    public int ClassifyId { get; init; }

    /// <summary>
    /// 是否公开.
    /// </summary>
    public bool IsPublic { get; init; } = default!;
}
