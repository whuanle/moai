using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using ModelContextProtocol.AspNetCore;

namespace MoAI.KnowledgeGraph;

/// <summary>
/// 知识图谱 MCP 服务器端点映射（/api/external/knowledge-graph/{kgId}/mcp）：
/// MCP streamable HTTP 传输（无状态模式，每个 JSON-RPC 请求独立鉴权），
/// 认证由 <c>ExternalAuthenticationMiddleware</c> 统一处理（要求接入 key/token 且勾选 kg_mcp 范围，kgId 须归属凭证团队）；
/// 工具域隔离（仅暴露知识图谱工具）由 <see cref="Mcp.KnowledgeGraphMcpToolGate"/> 按请求路径过滤.
/// </summary>
public static class KnowledgeGraphMcpEndpointMapper
{
    /// <summary>
    /// 映射知识图谱 MCP 服务器端点.
    /// </summary>
    /// <param name="app">端点路由构建器.</param>
    /// <returns>返回原构建器.</returns>
    public static IEndpointRouteBuilder MapKnowledgeGraphMcp(this IEndpointRouteBuilder app)
    {
        app.MapMcp("/api/external/knowledge-graph/{kgId}/mcp");
        return app;
    }
}
