using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Plugins.Mcp.Commands;

/// <summary>
/// 重置知识库 MCP 密钥.
/// </summary>
public class ResetWikiMcpKeyCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 知识库 Id.
    /// </summary>
    public int WikiId { get; init; }
}
