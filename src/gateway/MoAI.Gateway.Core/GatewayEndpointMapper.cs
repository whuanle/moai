using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MoAI.Gateway.Protocols.Inbound;
using MoAI.Gateway.Services;

namespace MoAI.Gateway;

/// <summary>
/// 网关 OpenAI/Anthropic 兼容端点映射（/v1/*，不走 /api 前缀约定）.
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
        app.MapPost("/v1/chat/completions", async (HttpContext http, GatewayChatCore core) =>
        {
            if (!await EnsureAuthenticatedAsync(http)) return;
            await core.HandleAsync(http, GatewayInboundFormat.OpenAIChatCompletions);
        }).AllowAnonymous();

        app.MapPost("/v1/responses", async (HttpContext http, GatewayChatCore core) =>
        {
            if (!await EnsureAuthenticatedAsync(http)) return;
            await core.HandleAsync(http, GatewayInboundFormat.OpenAIResponses);
        }).AllowAnonymous();

        app.MapPost("/v1/messages", async (HttpContext http, GatewayChatCore core) =>
        {
            if (!await EnsureAuthenticatedAsync(http)) return;
            await core.HandleAsync(http, GatewayInboundFormat.AnthropicMessages);
        }).AllowAnonymous();

        app.MapGet("/v1/models", async (HttpContext http, GatewayModelResolver resolver) =>
        {
            if (!await EnsureAuthenticatedAsync(http)) return;
            await GatewayModelsEndpoint.HandleAsync(http, resolver);
        }).AllowAnonymous();

        return app;
    }

    private static async Task<bool> EnsureAuthenticatedAsync(HttpContext http)
    {
        var result = await http.AuthenticateAsync(GatewayApiKeyDefaults.AuthenticationScheme);
        if (!result.Succeeded || result.Principal == null)
        {
            http.Response.StatusCode = 401;
            http.Response.ContentType = "application/json; charset=utf-8";
            await http.Response.WriteAsync("{\"error\":{\"message\":\"Invalid or missing API key.\",\"type\":\"authentication_error\",\"code\":\"invalid_api_key\"}}");
            return false;
        }

        http.User = result.Principal;
        return true;
    }
}
