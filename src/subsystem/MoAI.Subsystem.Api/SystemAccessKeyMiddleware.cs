using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Subsystem.Models;
using MoAI.Subsystem.Services;

namespace MoAI.Subsystem;

/// <summary>
/// 系统应用接入协作面认证中间件：守护 /api/subsys/system/**（token 换取端点除外），
/// 双轨凭据：① Bearer moai-sa- appid 直连（列内明文等值命中）；
/// ② Bearer JWT access token（appid+密钥经 /api/subsys/system/token 换取，typ=system_app_access）。
/// 每次请求回数据库实时解析（停用 403/删除 401 即时生效，scope 以库内当前为准），
/// 解析结果写入 HttpContext.Items 供控制器使用.
/// 必须注册在 <c>CustomAuthorizaMiddleware</c> 之前.
/// </summary>
public class SystemAccessKeyMiddleware : IMiddleware
{
    private const string SystemPathPrefix = "/api/subsys/system";

    private const string TokenPath = "/api/subsys/system/token";
    private const string TokenRefreshPath = "/api/subsys/system/token/refresh";

    private const string InvalidKeyMessage =
        "Invalid or missing system access credentials. Send 'Authorization: Bearer moai-sa-...' directly, or exchange an access token via POST /api/subsys/system/token with the appid and secret.";

    private readonly ISubsystemTokenProvider _subsystemTokenProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemAccessKeyMiddleware"/> class.
    /// </summary>
    /// <param name="subsystemTokenProvider">token 签发与校验服务.</param>
    public SystemAccessKeyMiddleware(ISubsystemTokenProvider subsystemTokenProvider)
    {
        _subsystemTokenProvider = subsystemTokenProvider;
    }

    /// <inheritdoc/>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var isSystemPath = path.Equals(SystemPathPrefix, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(SystemPathPrefix + "/", StringComparison.OrdinalIgnoreCase);
        if (!isSystemPath)
        {
            await next(context);
            return;
        }

        // token 换取/刷新为匿名端点直接放行（与 /api/subsys/token 同口径）
        if (path.Equals(TokenPath, StringComparison.OrdinalIgnoreCase)
            || path.Equals(TokenRefreshPath, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var bearer = ReadBearerToken(context);
        if (string.IsNullOrEmpty(bearer))
        {
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "authentication_error", "invalid_key", InvalidKeyMessage);
            return;
        }

        var databaseContext = context.RequestServices.GetRequiredService<DatabaseContext>();
        AccessAppEntity? app;

        if (bearer.StartsWith(SystemAccessAppAuthDefaults.KeyPrefix, StringComparison.Ordinal))
        {
            // 直连轨：appid 明文等值命中（全局软删过滤已排除删除行）
            app = await databaseContext.AccessApps
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Level == AccessAppLevel.System && x.Key == bearer, context.RequestAborted);
        }
        else if (_subsystemTokenProvider.TryValidateSystemAppAccessToken(bearer, out var appId, out var appKey))
        {
            // token 轨：按主体回库实时解析，scope/状态以库内当前为准
            app = await databaseContext.AccessApps
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Level == AccessAppLevel.System && x.Id == appId, context.RequestAborted);
            if (app != null && !string.Equals(app.Key, appKey, StringComparison.Ordinal))
            {
                app = null;
            }
        }
        else
        {
            app = null;
        }

        if (app == null)
        {
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "authentication_error", "invalid_key", InvalidKeyMessage);
            return;
        }

        if (app.Status == AccessAppStatus.Disabled)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "system_access_app_disabled", "The system access app is disabled.");
            return;
        }

        context.Items[SystemAccessAppAuthDefaults.CallerContextItemKey] = new SystemAccessAppCallerContext
        {
            Id = app.Id,
            Name = app.Name,
            Scopes = (SystemApiKeyScopes)app.Scopes,
            Status = app.Status,
        };
        await next(context);
    }

    private static string? ReadBearerToken(HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = authorization["Bearer ".Length..].Trim();
        return string.IsNullOrEmpty(token) ? null : token;
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string type, string code, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(new { error = new { message, type, code } });
    }
}
