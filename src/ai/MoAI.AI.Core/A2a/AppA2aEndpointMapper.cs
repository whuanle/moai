using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace MoAI.AI.A2a;

/// <summary>
/// 应用 A2A 端点映射（/api/external/app/{"{appId}"}/a2a）：认证与门禁由
/// ExternalAuthenticationMiddleware 完成（app_a2a 范围 + 外部用户语义 + 团队归属 + 已发布应用）；
/// POST 为 JSON-RPC（message/send、message/stream、tasks/get、tasks/cancel），
/// GET /a2a/agent.json 为 A2A Agent Card 发现.
/// </summary>
public static class AppA2aEndpointMapper
{
    /// <summary>
    /// 映射应用 A2A 端点.
    /// </summary>
    /// <param name="endpoints">端点路由构建器.</param>
    /// <returns>返回原构建器.</returns>
    public static IEndpointRouteBuilder MapAppA2aEndpoint(this IEndpointRouteBuilder endpoints)
    {
        // 注意 minimal API 端点不经过 MVC 的 /api 前缀 convention，模板需写完整路径
        endpoints.MapPost("/api/external/app/{appId:guid}/a2a", async (HttpContext context, Guid appId) =>
        {
            var server = context.RequestServices.GetRequiredService<AppA2aServer>();
            await server.HandleAsync(context, appId, context.RequestAborted);
        });

        endpoints.MapGet("/api/external/app/{appId:guid}/a2a/agent.json", async (HttpContext context, Guid appId) =>
        {
            var server = context.RequestServices.GetRequiredService<AppA2aServer>();
            await server.HandleAgentCardAsync(context, appId);
        });

        return endpoints;
    }
}
