using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using ModelContextProtocol.AspNetCore;

namespace MoAI.Wiki;

/// <summary>
/// 知识库 MCP 服务器端点映射（/api/external/wiki/{wikiId}/mcp）：
/// MCP streamable HTTP 传输（无状态模式，每个 JSON-RPC 请求独立鉴权），
/// 认证由 <c>ExternalAuthenticationMiddleware</c> 统一处理（要求接入 key/token 且勾选 wiki_mcp 范围，wikiId 须归属凭证团队）.
/// </summary>
public static class WikiMcpEndpointMapper
{
    /// <summary>
    /// 映射知识库 MCP 服务器端点.
    /// </summary>
    /// <param name="app">端点路由构建器.</param>
    /// <returns>返回原构建器.</returns>
    public static IEndpointRouteBuilder MapWikiMcp(this IEndpointRouteBuilder app)
    {
        app.MapMcp("/api/external/wiki/{wikiId}/mcp");
        return app;
    }
}
