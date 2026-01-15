using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Plugins.Mcp.Commands;

/// <summary>
/// 开启知识库 MCP 功能.
/// </summary>
public class EnableWikiMcpCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 知识库 Id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 是否开启，flase 是禁用.
    /// </summary>
    public bool IsEnable { get; init; }
}
