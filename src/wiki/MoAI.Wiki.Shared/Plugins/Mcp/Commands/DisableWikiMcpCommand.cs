using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Plugins.Mcp.Commands;

/// <summary>
/// 关闭知识库 MCP 功能.
/// </summary>
public class DisableWikiMcpCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 知识库 Id.
    /// </summary>
    public int WikiId { get; init; }
}
