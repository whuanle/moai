using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace MoAI.KnowledgeGraph.Mcp;

/// <summary>
/// MCP 端点工具门禁：宿主共用一个 McpServerOptions（知识库与知识图谱工具注册在同一 server），
/// 但 /api/external/wiki/{wikiId}/mcp 与 /api/external/knowledge-graph/{kgId}/mcp 两个端点各自只应暴露本域工具——
/// 通过 ListTools/CallTool 过滤器按请求路径分域（tools/list 收敛列表、tools/call 拒绝跨域调用）。
/// HttpContext 经静态 IHttpContextAccessor 读取（宿主 Program.cs 启动时 Initialize 一次）；
/// streamable HTTP 无状态模式下每个 JSON-RPC 请求都是独立 HTTP POST，AsyncLocal 上下文可靠.
/// </summary>
public static class KnowledgeGraphMcpToolGate
{
    private const string WikiDomain = "wiki";
    private const string KgDomain = "kg";

    private static readonly HashSet<string> WikiToolNames = new(StringComparer.Ordinal)
    {
        "list_knowledge_bases",
        "search_knowledge_base_files",
        "search_knowledge_base_recall",
    };

    private static readonly HashSet<string> KgToolNames = new(StringComparer.Ordinal)
    {
        "list_knowledge_graphs",
        "get_knowledge_graph_schema",
        "search_knowledge_graph_nodes",
        "search_knowledge_graph_recall",
    };

    private static IHttpContextAccessor? _accessor;

    /// <summary>
    /// 宿主启动时注入 IHttpContextAccessor（一次）.
    /// </summary>
    /// <param name="accessor">HTTP 上下文访问器.</param>
    public static void Initialize(IHttpContextAccessor accessor) => _accessor = accessor;

    /// <summary>
    /// 解析当前请求所属 MCP 域：wiki / kg / null（非 MCP 端点，不过滤）.
    /// </summary>
    /// <returns>返回域标识.</returns>
    public static string? CurrentDomain()
    {
        var path = _accessor?.HttpContext?.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/api/external/wiki", StringComparison.OrdinalIgnoreCase) && path.EndsWith("/mcp", StringComparison.OrdinalIgnoreCase))
        {
            return WikiDomain;
        }

        if (path.StartsWith("/api/external/knowledge-graph", StringComparison.OrdinalIgnoreCase) && path.EndsWith("/mcp", StringComparison.OrdinalIgnoreCase))
        {
            return KgDomain;
        }

        return null;
    }

    private static bool IsToolAllowed(string? toolName)
    {
        var domain = CurrentDomain();
        if (domain == null)
        {
            return true;
        }

        if (string.IsNullOrEmpty(toolName))
        {
            return false;
        }

        return domain == WikiDomain ? WikiToolNames.Contains(toolName) : KgToolNames.Contains(toolName);
    }

    /// <summary>
    /// 工具名是否属于任一域的已知工具；完全未知的工具不属于任何域，放行给 SDK 原生处理（未知工具报 -32602，保持无门禁时行为不变）.
    /// </summary>
    private static bool IsKnownTool(string? toolName)
        => !string.IsNullOrEmpty(toolName) && (WikiToolNames.Contains(toolName) || KgToolNames.Contains(toolName));

    /// <summary>
    /// tools/list 过滤：仅返回当前端点域的工具.
    /// </summary>
    public static readonly McpRequestFilter<ListToolsRequestParams, ListToolsResult> ListToolsFilter = next => async (request, cancellationToken) =>
    {
        var result = await next(request, cancellationToken);
        var domain = CurrentDomain();
        if (domain == null)
        {
            return result;
        }

        result.Tools = result.Tools.Where(t => IsToolAllowed(t.Name)).ToList();
        return result;
    };

    /// <summary>
    /// tools/call 过滤：仅拦截「已知但跨域」的工具（如 KG 端点调知识库工具）；
    /// 未知工具放行给 SDK 原生处理（-32602），与未上线门禁前行为一致.
    /// </summary>
    public static readonly McpRequestFilter<CallToolRequestParams, CallToolResult> CallToolFilter = next => async (request, cancellationToken) =>
    {
        if (IsKnownTool(request.Params.Name) && !IsToolAllowed(request.Params.Name))
        {
            throw new McpException($"工具 {request.Params.Name} 在当前 MCP 端点不可用，请检查接入地址对应的资源域.");
        }

        return await next(request, cancellationToken);
    };
}
