using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MoAI.Gateway.Protocols.Inbound;
using MoAI.Gateway.Services;

namespace MoAI.Gateway;

/// <summary>
/// 网关 OpenAI/Anthropic 兼容端点映射（/aiapi/{teamId}/v1/*，不走 /api 前缀约定）.
/// 团队 id 入路由，每个团队拥有独立接入地址；teamId 须与 API Key 绑定的团队完全一致.
/// </summary>
public static class GatewayEndpointMapper
{
    /// <summary>
    /// 映射网关端点.
    /// </summary>
    /// <param name="app">端点路由构建器.</param>
    /// <returns>返回原构建器.</returns>
    public static IEndpointRouteBuilder MapGatewayEndpoints(this IEndpointRouteBuilder app)
    {
        // 网关端点显式调用 ApiKey 认证方案，不依赖全局授权中间件：
        // CustomAuthorizaMiddleware 只识别 JWT 上下文，对 ApiKey principal 会误判为匿名.
        app.MapPost("/aiapi/{teamId}/v1/chat/completions", async (HttpContext http, int teamId, GatewayChatCore core) =>
        {
            if (!await EnsureTeamAuthenticatedAsync(http, teamId)) return;
            await core.HandleAsync(http, GatewayInboundFormat.OpenAIChatCompletions);
        }).AllowAnonymous();

        app.MapPost("/aiapi/{teamId}/v1/responses", async (HttpContext http, int teamId, GatewayChatCore core) =>
        {
            if (!await EnsureTeamAuthenticatedAsync(http, teamId)) return;
            await core.HandleAsync(http, GatewayInboundFormat.OpenAIResponses);
        }).AllowAnonymous();

        app.MapPost("/aiapi/{teamId}/v1/messages", async (HttpContext http, int teamId, GatewayChatCore core) =>
        {
            if (!await EnsureTeamAuthenticatedAsync(http, teamId)) return;
            await core.HandleAsync(http, GatewayInboundFormat.AnthropicMessages);
        }).AllowAnonymous();

        app.MapGet("/aiapi/{teamId}/v1/models", async (HttpContext http, int teamId, GatewayModelResolver resolver) =>
        {
            if (!await EnsureTeamAuthenticatedAsync(http, teamId)) return;
            await GatewayModelsEndpoint.HandleAsync(http, resolver);
        }).AllowAnonymous();

        return app;
    }

    private static async Task<bool> EnsureTeamAuthenticatedAsync(HttpContext http, int routeTeamId)
    {
        var result = await http.AuthenticateAsync(GatewayApiKeyDefaults.AuthenticationScheme);
        if (!result.Succeeded || result.Principal == null)
        {
            await WriteErrorAsync(http, 401, "Invalid or missing API key.", "authentication_error", "invalid_api_key");
            return false;
        }

        // 严格校验：路由 teamId 必须与密钥绑定的团队一致，不一致按未授权处理.
        var keyTeamId = int.TryParse(result.Principal.FindFirst("teamid")?.Value, out var t) ? t : 0;
        if (routeTeamId <= 0 || keyTeamId != routeTeamId)
        {
            await WriteErrorAsync(http, 403, "API key does not belong to this team.", "permission_error", "permission_denied");
            return false;
        }

        http.User = result.Principal;
        return true;
    }

    private static async Task WriteErrorAsync(HttpContext http, int statusCode, string message, string type, string code)
    {
        http.Response.StatusCode = statusCode;
        http.Response.ContentType = "application/json; charset=utf-8";
        await http.Response.WriteAsync($"{{\"error\":{{\"message\":\"{message}\",\"type\":\"{type}\",\"code\":\"{code}\"}}}}");
    }
}
