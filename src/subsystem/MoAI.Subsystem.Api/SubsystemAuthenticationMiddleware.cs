using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Subsystem.Models;
using MoAI.Subsystem.Services;

namespace MoAI.Subsystem;

/// <summary>
/// 子系统协作面认证中间件：守护 /api/subsys/heartbeat、/api/subsys/info、
/// /api/subsys/sso/exchange 与 /api/subsys/sso/refresh，
/// 凭据为 appid+密钥换取的子系统 token（audience = Server + |subsystem）；
/// 每次请求按 token 主体从数据库实时解析子系统（停用/删除即时生效），
/// 解析结果写入 HttpContext.Items 供控制器使用；/api/subsys/token 为匿名换取端点直接放行.
/// 必须注册在 <c>CustomAuthorizaMiddleware</c> 之前.
/// </summary>
public class SubsystemAuthenticationMiddleware : IMiddleware
{
    private const string TokenPath = "/api/subsys/token";
    private const string HeartbeatPath = "/api/subsys/heartbeat";
    private const string InfoPath = "/api/subsys/info";
    private const string SsoExchangePath = "/api/subsys/sso/exchange";
    private const string SsoRefreshPath = "/api/subsys/sso/refresh";

    private const string InvalidTokenMessage =
        "Invalid or missing subsystem token. Exchange an access token via POST /api/subsys/token with the subsystem key and secret.";

    private readonly ISubsystemTokenProvider _subsystemTokenProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsystemAuthenticationMiddleware"/> class.
    /// </summary>
    /// <param name="subsystemTokenProvider">子系统 token 服务.</param>
    public SubsystemAuthenticationMiddleware(ISubsystemTokenProvider subsystemTokenProvider)
    {
        _subsystemTokenProvider = subsystemTokenProvider;
    }

    /// <inheritdoc/>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // 仅守护协作面端点，管理面（/api/subsystem/list 等）与内部 SSO 签发走内部 JWT 鉴权不受影响
        var isMachinePath = path.Equals(HeartbeatPath, StringComparison.OrdinalIgnoreCase)
            || path.Equals(InfoPath, StringComparison.OrdinalIgnoreCase)
            || path.Equals(SsoExchangePath, StringComparison.OrdinalIgnoreCase)
            || path.Equals(SsoRefreshPath, StringComparison.OrdinalIgnoreCase);
        if (!isMachinePath)
        {
            await next(context);
            return;
        }

        var bearer = ReadBearerToken(context);
        if (string.IsNullOrEmpty(bearer) || !_subsystemTokenProvider.TryValidateAccessToken(bearer, out var subsystemId, out var subsystemKey))
        {
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "authentication_error", "invalid_token", InvalidTokenMessage);
            return;
        }

        // 实时库内校验：删除/改名/停用即时生效，不依赖 token 自然过期
        var databaseContext = context.RequestServices.GetRequiredService<DatabaseContext>();
        var subsystem = await databaseContext.Subsystems
            .FirstOrDefaultAsync(x => x.Id == subsystemId, context.RequestAborted);
        if (subsystem == null || !string.Equals(subsystem.Key, subsystemKey, StringComparison.Ordinal))
        {
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "authentication_error", "invalid_token", InvalidTokenMessage);
            return;
        }

        if (subsystem.Status == SubsystemStatus.Disabled)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "subsystem_disabled", "The subsystem is disabled.");
            return;
        }

        context.Items[SubsystemAuthDefaults.CallerContextItemKey] = SubsystemCallerContext.From(subsystem);
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
