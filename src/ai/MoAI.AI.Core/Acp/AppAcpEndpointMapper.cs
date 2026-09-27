using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace MoAI.AI.Acp;

/// <summary>
/// 应用 ACP 端点映射（/api/external/app/{"{appId}"}/acp）：认证与门禁由
/// ExternalAuthenticationMiddleware 完成（app_acp 范围 + 外部用户语义 + 团队归属 + 已发布应用）.
/// </summary>
public static class AppAcpEndpointMapper
{
    /// <summary>
    /// 映射应用 ACP 端点.
    /// </summary>
    /// <param name="endpoints">端点路由构建器.</param>
    /// <returns>返回原构建器.</returns>
    public static IEndpointRouteBuilder MapAppAcpEndpoint(this IEndpointRouteBuilder endpoints)
    {
        // 注意 minimal API 端点不经过 MVC 的 /api 前缀 convention，模板需写完整路径
        endpoints.MapPost("/api/external/app/{appId:guid}/acp", async (HttpContext context, Guid appId) =>
        {
            var server = context.RequestServices.GetRequiredService<AppAcpServer>();
            await server.HandleAsync(context, appId, context.RequestAborted);
        });

        return endpoints;
    }
}
