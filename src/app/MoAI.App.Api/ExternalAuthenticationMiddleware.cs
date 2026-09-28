using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using MoAI.App.Models;
using MoAI.App.Services;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;

namespace MoAI.App;

/// <summary>
/// 外部接入认证中间件：对 /api/external 请求做认证并写 HttpContext.User 与 <see cref="ExternalTokenContext"/>，
/// 支持两种凭证：
/// 1. 外部 token（POST /api/external/token 换取，audience = Server + |external）；
/// 2. key 直连：Authorization/x-api-key 直接携带应用接入 key（moai-ac-），
///    以 key 勾选范围访问团队资源；需要授权（is_auth）的应用会话面仍须换取外部用户 token。
/// 用户 token 的 external_user.id 落入 NameIdentifier，<c>AppAgentDispatcher</c> 等内部组件按既有
/// 「会话归属 = CreateUserId」逻辑零改动复用；应用 token / key 直连无数值主体，固定 0.
/// 必须注册在 <c>CustomAuthorizaMiddleware</c> 之前（后者会提前触发并缓存用户上下文）.
/// </summary>
public class ExternalAuthenticationMiddleware : IMiddleware
{
    private const string InvalidTokenMessage =
        "Invalid or missing external credential. Pass an external access token or an app access key (moai-ac-)"
        + " in the Authorization header; exchange a user token via POST /api/external/token for apps that require authorization.";

    private static readonly string[] AnonymousPaths =
    {
        "/api/external/token",
        "/api/external/token/refresh",
    };

    private readonly IExternalAccessKeyService _externalAccessKeyService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalAuthenticationMiddleware"/> class.
    /// </summary>
    /// <param name="externalAccessKeyService">key 直连解析服务.</param>
    public ExternalAuthenticationMiddleware(IExternalAccessKeyService externalAccessKeyService)
    {
        _externalAccessKeyService = externalAccessKeyService;
    }

    /// <inheritdoc/>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var path = context.Request.Path;
        if (!path.StartsWithSegments("/api/external", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        // 匿名端点：换取 token 与访问点公开配置（组件首次加载时未持有凭证）
        var isAnonymousPath =
            AnonymousPaths.Contains(path.Value, StringComparer.OrdinalIgnoreCase)
            || (path.StartsWithSegments("/api/external/app", StringComparison.OrdinalIgnoreCase)
                && path.Value!.EndsWith("/access-point", StringComparison.OrdinalIgnoreCase));

        var bearer = ReadBearerToken(context);
        var keyCredential = ExtractKeyCredential(context, bearer);

        ExternalTokenContext? tokenContext;
        if (keyCredential != null)
        {
            // key 直连：以 key 勾选范围构建上下文（应用接入 key）
            tokenContext = await _externalAccessKeyService.ResolveContextAsync(keyCredential, context.RequestAborted);
            if (tokenContext == null)
            {
                await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "authentication_error", "invalid_token", InvalidTokenMessage);
                return;
            }

            context.Items[ExternalAuthDefaults.TokenContextItemKey] = tokenContext;
            context.User = BuildPrincipal(tokenContext);
        }
        else if (string.IsNullOrEmpty(bearer))
        {
            if (!isAnonymousPath)
            {
                await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "authentication_error", "invalid_token", InvalidTokenMessage);
                return;
            }

            // 换 token 端点与匿名访问点直接放行，不产生外部身份
            await next(context);
            return;
        }
        else
        {
            // 外部 token
            var authenticateResult = await context.AuthenticateAsync(ExternalAuthDefaults.AuthenticationScheme);
            if (!authenticateResult.Succeeded || authenticateResult.Principal == null)
            {
                await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "authentication_error", "invalid_token", InvalidTokenMessage);
                return;
            }

            context.User = authenticateResult.Principal;
            tokenContext = context.Items.TryGetValue(ExternalAuthDefaults.TokenContextItemKey, out var value) ? value as ExternalTokenContext : null;
            if (tokenContext == null)
            {
                await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "authentication_error", "invalid_token", InvalidTokenMessage);
                return;
            }
        }

        // 资源范围门禁（前置拦截）：知识库/知识图谱外部接口按路径+方法静态分档，
        // token 与 key 直连同口径，未勾选对应范围在中间件层直接 403（控制器内保留同口径校验兜底）
        var requiredScope = GetRequiredResourceScope(path, context.Request.Method);
        if (requiredScope.HasValue && !tokenContext.Scopes.HasFlag(requiredScope.Value))
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "insufficient_scope",
                "The credential does not have the required scope for this API.");
            return;
        }

        // 所有携带 appId 路由值的外部端点（/api/external/agent/{appId}/...、access-point 等）：
        // 校验 appId 属于凭证归属团队（团队级授权）且应用可用；需要授权（is_auth）的应用会话面
        // 仅外部用户 token 可进入，key 直连/应用 token 一律 403 引导换取用户 token。
        // 应用 ACP/A2A 端点不走此通用门禁：内部应用（非外部应用）同样开放 ACP/A2A，
        // 由下方各自专属门禁接管
        Guid? routeAppId = null;
        if (context.Request.RouteValues.TryGetValue("appId", out var appIdValue) && appIdValue != null)
        {
            if (!Guid.TryParse(appIdValue.ToString(), out var appId))
            {
                await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "permission_denied", "You do not have access to this application.");
                return;
            }

            routeAppId = appId;
            if (!IsAppAcpPath(path) && !IsAppA2aPath(path))
            {
                var allowed = await IsAppAllowedAsync(context, appId, tokenContext);
                if (!allowed)
                {
                    return;
                }
            }
        }

        // 知识库 MCP 端点（/api/external/wiki/{wikiId}/mcp）：仅接入 key / 应用 token（无外部用户语义），
        // 要求勾选 wiki_mcp 范围，且路由 wikiId 须归属凭证团队（工具均为只读，不再叠加 wiki_read 要求）。
        // 无状态传输仅支持 POST JSON-RPC，GET（SSE 监听）/DELETE（会话终止）按协议要求 405
        if (IsWikiMcpPath(path))
        {
            if (!HttpMethods.IsPost(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                return;
            }

            var mcpAllowed = await IsWikiMcpAllowedAsync(context, tokenContext!);
            if (!mcpAllowed)
            {
                return;
            }
        }

        // 知识图谱 MCP 端点（/api/external/knowledge-graph/{kgId}/mcp）：与知识库 MCP 同口径，
        // 要求勾选 kg_mcp 范围（不叠加 kg_read），路由 kgId 须归属凭证团队
        if (IsKgMcpPath(path))
        {
            if (!HttpMethods.IsPost(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                return;
            }

            var kgMcpAllowed = await IsKgMcpAllowedAsync(context, tokenContext!);
            if (!kgMcpAllowed)
            {
                return;
            }
        }

        // 应用 ACP 端点（/api/external/app/{appId}/acp，agent-to-agent）：仅 POST JSON-RPC。
        // key 直连先解析直连会话身份（ACP 会话归属需要 external_user.id，与对话面同口径），
        // 再按 ACP 专属门禁放行（app_acp 范围 + 应用归属团队 + 已发布；不要求 IsExternal，内部应用同样开放）
        if (IsAppAcpPath(path))
        {
            if (!HttpMethods.IsPost(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                return;
            }

            if (tokenContext!.IsKeyDirect)
            {
                var external = await _externalAccessKeyService.EnsurePrincipalUserAsync(
                    (int)tokenContext.TeamId,
                    tokenContext.AccessAppId!.Value,
                    routeAppId,
                    context.RequestAborted);

                tokenContext = WithExternalUser(tokenContext, external.Id, external.ExternalUserId);
                context.Items[ExternalAuthDefaults.TokenContextItemKey] = tokenContext;
                context.User = BuildPrincipal(tokenContext);
            }

            var acpAllowed = await IsAppAcpAllowedAsync(context, routeAppId, tokenContext!);
            if (!acpAllowed)
            {
                return;
            }
        }

        // 应用 A2A 端点（/api/external/app/{appId}/a2a[/**]，Google Agent2Agent）：
        // POST = JSON-RPC（message/send、message/stream、tasks/get、tasks/cancel）；GET = Agent Card 发现。
        // 门禁与 ACP 同口径：key 直连先解析直连会话身份（会话归属需要 external_user.id），
        // 再按 app_a2a 范围 + 应用归属团队 + 已发布放行
        if (IsAppA2aPath(path))
        {
            if (!HttpMethods.IsPost(context.Request.Method) && !HttpMethods.IsGet(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                return;
            }

            if (tokenContext!.IsKeyDirect)
            {
                var external = await _externalAccessKeyService.EnsurePrincipalUserAsync(
                    (int)tokenContext.TeamId,
                    tokenContext.AccessAppId!.Value,
                    routeAppId,
                    context.RequestAborted);

                tokenContext = WithExternalUser(tokenContext, external.Id, external.ExternalUserId);
                context.Items[ExternalAuthDefaults.TokenContextItemKey] = tokenContext;
                context.User = BuildPrincipal(tokenContext);
            }

            var a2aAllowed = await IsAppA2aAllowedAsync(context, routeAppId, tokenContext!);
            if (!a2aAllowed)
            {
                return;
            }
        }

        // key 直连的会话面端点（建会话/会话列表/对话/会话消息）：需要外部用户 id 作会话归属，
        // 应用接入 key 以「直连会话身份」外部用户承载
        if (tokenContext!.IsKeyDirect && IsConversationPath(path))
        {
            var external = await _externalAccessKeyService.EnsurePrincipalUserAsync(
                (int)tokenContext.TeamId,
                tokenContext.AccessAppId.Value,
                routeAppId,
                context.RequestAborted);

            tokenContext = WithExternalUser(tokenContext, external.Id, external.ExternalUserId);
            context.Items[ExternalAuthDefaults.TokenContextItemKey] = tokenContext;
            context.User = BuildPrincipal(tokenContext);
        }

        await next(context);
    }

    /// <summary>
    /// 提取 key 直连凭证：Bearer 值或 x-api-key 以应用接入 key 前缀（moai-ac-）开头时按 key 处理，外部 JWT 不会命中.
    /// </summary>
    private static string? ExtractKeyCredential(HttpContext context, string? bearer)
    {
        foreach (var candidate in new[] { bearer, context.Request.Headers["x-api-key"].ToString().Trim() })
        {
            if (!string.IsNullOrEmpty(candidate) && candidate.StartsWith(ExternalAuthDefaults.AccessAppKeyPrefix, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsConversationPath(PathString path)
    {
        return path.StartsWithSegments("/api/external/agent", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/external/session", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 是否为知识库 MCP 端点：/api/external/wiki/{wikiId}/mcp（外部 wiki 其余子路径不受影响）.
    /// </summary>
    private static bool IsWikiMcpPath(PathString path)
    {
        return path.StartsWithSegments("/api/external/wiki", StringComparison.OrdinalIgnoreCase)
            && path.Value!.EndsWith("/mcp", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 是否为知识图谱 MCP 端点：/api/external/knowledge-graph/{kgId}/mcp（外部知识图谱其余子路径不受影响）.
    /// </summary>
    private static bool IsKgMcpPath(PathString path)
    {
        return path.StartsWithSegments("/api/external/knowledge-graph", StringComparison.OrdinalIgnoreCase)
            && path.Value!.EndsWith("/mcp", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 是否为应用 ACP 端点：/api/external/app/{"{appId}"}/acp（app/list、access-point 等其余子路径不受影响）.
    /// </summary>
    private static bool IsAppAcpPath(PathString path)
    {
        return path.StartsWithSegments("/api/external/app", StringComparison.OrdinalIgnoreCase)
            && path.Value!.EndsWith("/acp", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 是否为应用 A2A 端点：/api/external/app/{"{appId}"}/a2a（JSON-RPC）与其 Agent Card 子路径 /a2a/agent.json.
    /// </summary>
    private static bool IsAppA2aPath(PathString path)
    {
        if (!path.StartsWithSegments("/api/external/app", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var value = path.Value!;
        return value.EndsWith("/a2a", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith("/a2a/agent.json", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 应用 ACP 端点授权：凭证须带外部用户语义（用户 token 或已解析的 key 直连身份，承载会话归属），
    /// scope 勾选 app_acp，路由 appId 存在、归属凭证团队（跨团队一律 404，不泄露存在性）且已发布可用.
    /// </summary>
    private static async Task<bool> IsAppAcpAllowedAsync(HttpContext context, Guid? appId, ExternalTokenContext tokenContext)
    {
        if (tokenContext.ExternalId <= 0)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "external_user_token_unsupported",
                "Application ACP only accepts an app access key (moai-ac-) or an external user token; sessions need an external user identity.");
            return false;
        }

        if (!tokenContext.Scopes.HasFlag(TeamApiKeyScopes.AppAcp))
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "insufficient_scope",
                "The credential does not have the app_acp scope. Enable it on the access app.");
            return false;
        }

        if (appId == null)
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, "not_found_error", "app_not_found", "Application not found.");
            return false;
        }

        var databaseContext = context.RequestServices.GetRequiredService<DatabaseContext>();
        var app = await databaseContext.Apps
            .Where(x => x.Id == appId.Value)
            .Select(x => new { x.TeamId, x.IsDisable, x.PublishStatus })
            .FirstOrDefaultAsync(context.RequestAborted);

        if (app == null || (long)app.TeamId != tokenContext.TeamId)
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, "not_found_error", "app_not_found", "Application not found.");
            return false;
        }

        if (app.IsDisable || app.PublishStatus != 1)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "permission_denied", "Application is unavailable.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 应用 A2A 端点授权：凭证须带外部用户语义（用户 token 或已解析的 key 直连身份，承载会话归属），
    /// scope 勾选 app_a2a，路由 appId 存在、归属凭证团队（跨团队一律 404，不泄露存在性）且已发布可用.
    /// </summary>
    private static async Task<bool> IsAppA2aAllowedAsync(HttpContext context, Guid? appId, ExternalTokenContext tokenContext)
    {
        if (tokenContext.ExternalId <= 0)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "external_user_token_unsupported",
                "Application A2A only accepts an app access key (moai-ac-) or an external user token; sessions need an external user identity.");
            return false;
        }

        if (!tokenContext.Scopes.HasFlag(TeamApiKeyScopes.AppA2a))
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "insufficient_scope",
                "The credential does not have the app_a2a scope. Enable it on the access app.");
            return false;
        }

        if (appId == null)
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, "not_found_error", "app_not_found", "Application not found.");
            return false;
        }

        var databaseContext = context.RequestServices.GetRequiredService<DatabaseContext>();
        var app = await databaseContext.Apps
            .Where(x => x.Id == appId.Value)
            .Select(x => new { x.TeamId, x.IsDisable, x.PublishStatus })
            .FirstOrDefaultAsync(context.RequestAborted);

        if (app == null || (long)app.TeamId != tokenContext.TeamId)
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, "not_found_error", "app_not_found", "Application not found.");
            return false;
        }

        if (app.IsDisable || app.PublishStatus != 1)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "permission_denied", "Application is unavailable.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 知识图谱 MCP 端点授权：凭证主体须为接入 key / 应用 token（外部用户 token 无 MCP 语义），
    /// scope 勾选 kg_mcp，路由 kgId 存在且归属凭证团队（跨团队一律 404，不泄露存在性）.
    /// </summary>
    private static async Task<bool> IsKgMcpAllowedAsync(HttpContext context, ExternalTokenContext tokenContext)
    {
        if (tokenContext.SubjectType != UserType.ExternalApp)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "external_user_token_unsupported",
                "Knowledge graph MCP only accepts an app access key (moai-ac-) with the kg_mcp scope.");
            return false;
        }

        if (!tokenContext.Scopes.HasFlag(TeamApiKeyScopes.KgMcp))
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "insufficient_scope",
                "The credential does not have the kg_mcp scope. Enable it on the access app.");
            return false;
        }

        if (!context.Request.RouteValues.TryGetValue("kgId", out var kgIdValue) || !long.TryParse(kgIdValue?.ToString(), out var kgId) || kgId <= 0)
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, "not_found_error", "kg_not_found", "Knowledge graph not found.");
            return false;
        }

        var databaseContext = context.RequestServices.GetRequiredService<DatabaseContext>();
        var kgTeamId = await databaseContext.KnowledgeGraphs
            .Where(x => x.Id == kgId)
            .Select(x => (long?)x.TeamId)
            .FirstOrDefaultAsync(context.RequestAborted);

        if (kgTeamId == null || kgTeamId != tokenContext.TeamId)
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, "not_found_error", "kg_not_found", "Knowledge graph not found.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 知识库 MCP 端点授权：凭证主体须为接入 key / 应用 token（外部用户 token 无 MCP 语义），
    /// scope 勾选 wiki_mcp，路由 wikiId 存在且归属凭证团队（跨团队一律 404，不泄露存在性）.
    /// </summary>
    private static async Task<bool> IsWikiMcpAllowedAsync(HttpContext context, ExternalTokenContext tokenContext)
    {
        if (tokenContext.SubjectType != UserType.ExternalApp)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "external_user_token_unsupported",
                "Knowledge base MCP only accepts an app access key (moai-ac-) with the wiki_mcp scope.");
            return false;
        }

        if (!tokenContext.Scopes.HasFlag(TeamApiKeyScopes.WikiMcp))
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "insufficient_scope",
                "The credential does not have the wiki_mcp scope. Enable it on the access app.");
            return false;
        }

        if (!context.Request.RouteValues.TryGetValue("wikiId", out var wikiIdValue) || !long.TryParse(wikiIdValue?.ToString(), out var wikiId) || wikiId <= 0)
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, "not_found_error", "wiki_not_found", "Knowledge base not found.");
            return false;
        }

        var databaseContext = context.RequestServices.GetRequiredService<DatabaseContext>();
        var wikiTeamId = await databaseContext.Wikis
            .Where(x => x.Id == wikiId)
            .Select(x => (long?)x.TeamId)
            .FirstOrDefaultAsync(context.RequestAborted);

        if (wikiTeamId == null || wikiTeamId != tokenContext.TeamId)
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, "not_found_error", "wiki_not_found", "Knowledge base not found.");
            return false;
        }

        return true;
    }

    private static async Task<bool> IsAppAllowedAsync(HttpContext context, Guid appId, ExternalTokenContext? tokenContext)
    {
        if (tokenContext == null)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "permission_denied", "You do not have access to this application.");
            return false;
        }

        var databaseContext = context.RequestServices.GetRequiredService<DatabaseContext>();
        var app = await databaseContext.Apps
            .Where(x => x.Id == appId)
            .Select(x => new { x.TeamId, x.IsExternal, x.IsDisable, x.PublishStatus, x.IsAuth })
            .FirstOrDefaultAsync(context.RequestAborted);

        if (app == null || !app.IsExternal)
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, "not_found_error", "app_not_found", "Application not found.");
            return false;
        }

        // 团队级授权：appId 必须属于凭证归属团队
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

        // 访问点公开配置不设授权门禁（组件加载时需要读取 is_auth 判断是否走授权流程）
        var isAccessPointPath = context.Request.Path.StartsWithSegments("/api/external/app", StringComparison.OrdinalIgnoreCase)
            && context.Request.Path.Value!.EndsWith("/access-point", StringComparison.OrdinalIgnoreCase);
        if (tokenContext.ExternalId <= 0 && app.IsAuth && !isAccessPointPath)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "external_user_token_required",
                "This application requires authorization. Exchange an external user token first via POST /api/external/token.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 构造外部身份 principal：携带外部用户 id（用户 token 或已解析的 key 直连身份）时落 external_user.id，其余固定 0（匿名语义）.
    /// </summary>
    private static ClaimsPrincipal BuildPrincipal(ExternalTokenContext tokenContext)
    {
        var claims = new List<Claim>
        {
            new("subject_type", tokenContext.SubjectType.ToString()),
            new("subject_id", tokenContext.SubjectId),
            new(ClaimTypes.Name, tokenContext.Nickname ?? tokenContext.SubjectId),
            new(ClaimTypes.NameIdentifier, (tokenContext.ExternalId > 0 ? tokenContext.ExternalId : 0).ToString()),
            new(JwtRegisteredClaimNames.Typ, tokenContext.SubjectType.ToJsonString()),
        };

        if (!string.IsNullOrEmpty(tokenContext.Nickname))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Nickname, tokenContext.Nickname));
        }

        var identity = new ClaimsIdentity(claims, ExternalAuthDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    private static ExternalTokenContext WithExternalUser(ExternalTokenContext tokenContext, long externalId, string externalUserId)
    {
        return new ExternalTokenContext
        {
            SubjectType = tokenContext.SubjectType,
            SubjectId = tokenContext.SubjectId,
            ExternalId = externalId,
            TeamId = tokenContext.TeamId,
            AccessAppId = tokenContext.AccessAppId,
            Scopes = tokenContext.Scopes,
            AppId = tokenContext.AppId,
            ExternalUserId = externalUserId,
            Nickname = tokenContext.Nickname,
            IsKeyDirect = tokenContext.IsKeyDirect,
        };
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

    /// <summary>
    /// 按路径+方法静态判定外部资源接口所需范围：GET 与 */list 查询归读档，其余归写档；
    /// 知识库 MCP 端点另行要求 wiki_mcp（见 IsWikiMcpPath），不叠加读写档；非资源类路径返回 null 不设限.
    /// </summary>
    /// <param name="path">请求路径.</param>
    /// <param name="method">HTTP 方法.</param>
    /// <returns>返回要求的范围位，null=不设限.</returns>
    private static TeamApiKeyScopes? GetRequiredResourceScope(PathString path, string method)
    {
        if (path.StartsWithSegments("/api/external/wiki", StringComparison.OrdinalIgnoreCase))
        {
            if (IsWikiMcpPath(path))
            {
                return null;
            }

            return IsResourceReadRequest(path, method) ? TeamApiKeyScopes.WikiRead : TeamApiKeyScopes.WikiWrite;
        }

        if (path.StartsWithSegments("/api/external/knowledge-graph", StringComparison.OrdinalIgnoreCase))
        {
            // 知识图谱 MCP 端点另行要求 kg_mcp（见 IsKgMcpPath），不叠加读写档
            if (IsKgMcpPath(path))
            {
                return null;
            }

            return IsResourceReadRequest(path, method) ? TeamApiKeyScopes.KgRead : TeamApiKeyScopes.KgWrite;
        }

        return null;
    }

    private static bool IsResourceReadRequest(PathString path, string method)
    {
        if (HttpMethods.IsGet(method))
        {
            return true;
        }

        // POST 形态的查询端点：/wiki/list、/documents/list、/nodes/list、/edges/list 等
        return path.Value!.EndsWith("/list", StringComparison.OrdinalIgnoreCase);
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
