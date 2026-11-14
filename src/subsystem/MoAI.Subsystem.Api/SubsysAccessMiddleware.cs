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
/// 系统级接入协作面认证中间件：守护 /api/subsys/**（token 换取/刷新端点除外），
/// 子系统与应用接入共用一套鉴权，双轨凭据：
/// ① Bearer moai-sa- appid 直连（列内明文等值命中，仅自动生成的应用接入行可命中）；
/// ② Bearer JWT 机器 token（appid+密钥经 /api/subsys/token 换取，typ=access_token）.
/// 每次请求回数据库实时解析（停用 403/删除或 appid 不匹配 401 即时生效），
/// 解析结果写入 HttpContext.Items 供控制器使用.
/// 必须注册在 <c>CustomAuthorizaMiddleware</c> 之前.
/// </summary>
public class SubsysAccessMiddleware : IMiddleware
{
    private const string SubsysPathPrefix = "/api/subsys";

    private const string TokenPath = "/api/subsys/token";
    private const string TokenRefreshPath = "/api/subsys/token/refresh";

    private const string InvalidTokenMessage =
        "Invalid or missing access credentials. Send 'Authorization: Bearer moai-sa-...' directly (app access only), or exchange an access token via POST /api/subsys/token with the appid and secret.";

    private readonly ISubsystemTokenProvider _subsystemTokenProvider;
    private readonly ISubsystemAppIdCodec _appIdCodec;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsysAccessMiddleware"/> class.
    /// </summary>
    /// <param name="subsystemTokenProvider">token 签发与校验服务.</param>
    /// <param name="appIdCodec">结构化 appid 编解码服务.</param>
    public SubsysAccessMiddleware(ISubsystemTokenProvider subsystemTokenProvider, ISubsystemAppIdCodec appIdCodec)
    {
        _subsystemTokenProvider = subsystemTokenProvider;
        _appIdCodec = appIdCodec;
    }

    /// <inheritdoc/>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var isSubsysPath = path.Equals(SubsysPathPrefix, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(SubsysPathPrefix + "/", StringComparison.OrdinalIgnoreCase);
        if (!isSubsysPath)
        {
            await next(context);
            return;
        }

        // token 换取/刷新为匿名端点直接放行
        if (path.Equals(TokenPath, StringComparison.OrdinalIgnoreCase)
            || path.Equals(TokenRefreshPath, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var bearer = ReadBearerToken(context);
        AccessAppEntity? app = null;

        if (string.IsNullOrEmpty(bearer))
        {
            // 保持 app == null，走统一 401
        }
        else if (bearer.StartsWith(SubsystemAuthDefaults.KeyPrefixSystemApp, StringComparison.Ordinal))
        {
            var databaseContext = context.RequestServices.GetRequiredService<DatabaseContext>();
            if (_appIdCodec.TryDecode(bearer, out var appId))
            {
                // 结构化轨：本地 HMAC 校验通过才打库，按主键点查（伪造/打错/扫描请求不打数据库）
                app = await databaseContext.AccessApps
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Level == AccessAppLevel.System && x.Id == appId, context.RequestAborted);
            }
            else if (bearer.Length == SubsystemAuthDefaults.LegacySystemAppKeyLength && SubsystemAuthDefaults.IsValidLegacySystemAppKey(bearer))
            {
                // 存量随机格式（40 字符）：Key 明文等值命中
                app = await databaseContext.AccessApps
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Level == AccessAppLevel.System && x.Key == bearer, context.RequestAborted);
            }

            // 其余格式：app 保持 null，走统一 401，不打数据库
        }
        else if (bearer.Count(x => x == '.') == 2 && _subsystemTokenProvider.TryValidateAccessToken(bearer, out var appId, out var appKey))
        {
            // token 轨：按主体回库实时解析，状态以库内当前为准
            var databaseContext = context.RequestServices.GetRequiredService<DatabaseContext>();
            app = await databaseContext.AccessApps
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Level == AccessAppLevel.System && x.Id == appId, context.RequestAborted);
            if (app != null && !string.Equals(app.Key, appKey, StringComparison.Ordinal))
            {
                app = null;
            }
        }

        if (app == null)
        {
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "authentication_error", "invalid_token", InvalidTokenMessage);
            return;
        }

        if (app.Status == AccessAppStatus.Disabled)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "subsys_app_disabled", "The access app is disabled.");
            return;
        }

        context.Items[SubsystemAuthDefaults.CallerContextItemKey] = SubsysCallerContext.From(app);
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
