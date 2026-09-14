using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace MoAI.AI;

/// <summary>
/// Agent 对话的 AG-UI 端点映射（/api/app/{appId}/chat，SSE 流）.
/// </summary>
public static class AppAgentEndpointMapper
{
    /// <summary>
    /// 映射 Agent 应用对话端点.
    /// </summary>
    /// <param name="endpoints">端点路由构建器.</param>
    /// <returns>返回原构建器.</returns>
    public static IEndpointRouteBuilder MapAppAgentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapAGUIServer(AppAgentConstants.AgentName, "/api/agent/{appId:guid}/chat")
            .RequireAuthorization();

        // 外部应用对话端点：同一 Agent 与会话存储，认证/授权范围由 ExternalAuthenticationMiddleware
        // （外部 token + appId ∈ 授权范围 + 应用可用）在管道中完成。
        // 注意 AG-UI 端点不经过 MVC 的 /api 前缀 convention，模板需写完整路径
        endpoints
            .MapAGUIServer(AppAgentConstants.AgentName, "/api/external/agent/{appId:guid}/chat");

        return endpoints;
    }
}
