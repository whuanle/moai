using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoAI.App.Models;
using MoAI.App.Services;
using MoAI.Database;

namespace MoAI.App;

/// <summary>
/// 外部接入认证中间件：对 /api/external 请求用外部认证方案（<see cref="ExternalAuthDefaults.AuthenticationScheme"/>）
/// 认证并写 HttpContext.User，使 <c>UserContextProvider</c> 能解析出 external_user.id（用户 token），
/// 从而 <c>AppAgentDispatcher</c> 等内部组件按既有「会话归属 = CreateUserId」逻辑零改动复用.
/// 必须注册在 <c>CustomAuthorizaMiddleware</c> 之前（后者会提前触发并缓存用户上下文）.
/// </summary>
public class ExternalAuthenticationMiddleware : IMiddleware
{
    private static readonly string[] AnonymousPaths =
    {
        "/api/external/token",
        "/api/external/token/refresh",
    };

    /// <inheritdoc/>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var path = context.Request.Path;
        if (!path.StartsWithSegments("/api/external", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        // 匿名端点：换取 token 与访问点公开配置（组件首次加载时未持有 token）
        var isAnonymousPath =
            AnonymousPaths.Contains(path.Value, StringComparer.OrdinalIgnoreCase)
            || (path.StartsWithSegments("/api/external/app", StringComparison.OrdinalIgnoreCase)
                && path.Value!.EndsWith("/access-point", StringComparison.OrdinalIgnoreCase));

        var hasBearer = !string.IsNullOrEmpty(context.Request.Headers.Authorization);

        if (!hasBearer)
        {
            // 换 token 端点匿名放行；其余外部端点必须携带外部 token
            if (isAnonymousPath)
            {
                await next(context);
                return;
            }

            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "authentication_error", "invalid_token", "Invalid or missing external token.");
            return;
        }

        var authenticateResult = await context.AuthenticateAsync(ExternalAuthDefaults.AuthenticationScheme);
        if (!authenticateResult.Succeeded || authenticateResult.Principal == null)
        {
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "authentication_error", "invalid_token", "Invalid or missing external token.");
            return;
        }

        context.User = authenticateResult.Principal;

        // 所有携带 appId 路由值的外部端点（/api/external/agent/{appId}/...：建会话、会话列表、对话等）：
        // 校验 appId 属于 token 归属团队（团队级授权）且应用可用；会话归属由各 Handler/AppAgentDispatcher 按外部用户 id 校验，此处只做授权范围守卫
        if (context.Request.RouteValues.TryGetValue("appId", out var appIdValue) && appIdValue != null)
        {
            var allowed = await IsChatAllowedAsync(context, appIdValue.ToString());
            if (!allowed)
            {
                return;
            }
        }

        await next(context);
    }

    private static async Task<bool> IsChatAllowedAsync(HttpContext context, string? appIdRaw)
    {
        if (!Guid.TryParse(appIdRaw, out var appId))
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "permission_denied", "You do not have access to this application.");
            return false;
        }

        var tokenContext = context.Items.TryGetValue(ExternalAuthDefaults.TokenContextItemKey, out var value) ? value as ExternalTokenContext : null;
        if (tokenContext == null)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "permission_denied", "You do not have access to this application.");
            return false;
        }

        var databaseContext = context.RequestServices.GetRequiredService<DatabaseContext>();
        var app = await databaseContext.Apps
            .Where(x => x.Id == appId)
            .Select(x => new { x.TeamId, x.IsExternal, x.IsDisable, x.PublishStatus })
            .FirstOrDefaultAsync(context.RequestAborted);

        if (app == null || !app.IsExternal)
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, "not_found_error", "app_not_found", "Application not found.");
            return false;
        }

        // 团队级授权：appId 必须属于 token 归属团队
        if ((long)app.TeamId != tokenContext.TeamId)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "permission_denied", "You do not have access to this application.");
            return false;
        }

        if (app.IsDisable || app.PublishStatus != 1)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "permission_denied", "Application is unavailable.");
            return false;
        }

        return true;
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string type, string code, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(new
        {
            error = new
            {
                message,
                type,
                code,
            }
        });
    }
}
