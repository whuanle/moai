using MediatR;
using MoAI.Wiki.Plugins.Mcp.Models;

namespace MoAI.Wiki.Plugins.Mcp.Queries;

/// <summary>
/// 查询知识库 MCP 配置信息.
/// </summary>
public class QueryWikiMcpConfigCommand : IRequest<QueryWikiMcpConfigCommandResponse>
{
    /// <summary>
    /// 知识库 Id.
    /// </summary>
    public int WikiId { get; init; }
}
