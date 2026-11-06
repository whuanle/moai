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
/// 外部接入认证中间件：对 /api/teamres 请求做认证并写 HttpContext.User 与 <see cref="ExternalTokenContext"/>，
/// 支持三种凭证：
/// 1. 外部 token（POST /api/teamres/token 换取，audience = Server + |external）；
/// 2. key 直连：Authorization/x-api-key 直接携带应用接入 key（moai-ac-），团队级全量访问 /api/teamres；
/// 3. 团队资源 key（moai-tr-）：仅能访问绑定资源的端点（应用 ACP/A2A、知识库 REST+MCP、知识图谱 REST+MCP）。
/// 用户 token 的 external_user.id 落入 NameIdentifier，<c>AppAgentDispatcher</c> 等内部组件按既有
/// 「会话归属 = CreateUserId」逻辑零改动复用；应用 token / key 直连无数值主体，固定 0.
/// 必须注册在 <c>CustomAuthorizaMiddleware</c> 之前（后者会提前触发并缓存用户上下文）.
/// </summary>
public class ExternalAuthenticationMiddleware : IMiddleware
{
    private const string InvalidTokenMessage =
        "Invalid or missing external credential. Pass an external access token, an app access key (moai-ac-)"
        + " or a resource key (moai-tr-) in the Authorization header; exchange a user token via POST /api/teamres/token for apps that require authorization.";

    private static readonly string[] AnonymousPaths =
    {
        "/api/teamres/token",
        "/api/teamres/token/refresh",
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
        if (!path.StartsWithSegments("/api/teamres", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        // 匿名端点：换取 token 与访问点公开配置（组件首次加载时未持有凭证）
        var isAnonymousPath =
            AnonymousPaths.Contains(path.Value, StringComparer.OrdinalIgnoreCase)
            || (path.StartsWithSegments("/api/teamres/app", StringComparison.OrdinalIgnoreCase)
                && path.Value!.EndsWith("/access-point", StringComparison.OrdinalIgnoreCase));

        var bearer = ReadBearerToken(context);
        var keyCredential = ExtractKeyCredential(context, bearer);

        ExternalTokenContext? tokenContext;
        if (keyCredential != null)
        {
            // key 直连：应用接入 key（moai-ac-）团队级全量 / 团队资源 key（moai-tr-）绑定资源
            tokenContext = await _externalAccessKeyService.ResolveContextAsync(keyCredential, context.RequestAborted);
            if (tokenContext == null)
            {
                await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "authentication_error", "invalid_token", InvalidTokenMessage);
                return;
            }

            context.Items[ExternalAuthDefaults.TokenContextItemKey] = tokenContext;
            context.User = BuildPrincipal(tokenContext);

            // 资源 key 路径包围圈（前置拦截）：只能访问绑定资源的端点，且绑定资源须已开启对应功能；越圈 403
            if (tokenContext.IsResourceKey && !await IsResourceKeyAllowedAsync(context, path, tokenContext))
            {
                return;
            }
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

        // 所有携带 appId 路由值的外部端点（/api/teamres/agent/{appId}/...、access-point 等）：
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

        // 知识库 MCP 端点（/api/teamres/wiki/{wikiId}/mcp）：仅接入 key / 应用 token / 该库资源 key（无外部用户语义），
        // 路由 wikiId 须归属凭证团队（工具均为只读）。
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

        // 知识图谱 MCP 端点（/api/teamres/knowledge-graph/{kgId}/mcp）：与知识库 MCP 同口径，
        // 路由 kgId 须归属凭证团队
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

        // 应用 ACP 端点（/api/teamres/app/{appId}/acp，agent-to-agent）：仅 POST JSON-RPC。
        // key 直连先解析直连会话身份（ACP 会话归属需要 external_user.id，与对话面同口径），
        // 再按 ACP 专属门禁放行（应用归属团队 + 已发布；不要求 IsExternal，内部应用同样开放）
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
                    tokenContext.AccessAppId,
                    routeAppId,
                    KeyDirectMarker(tokenContext),
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

        // 应用 A2A 端点（/api/teamres/app/{appId}/a2a[/**]，Google Agent2Agent）：
        // POST = JSON-RPC（message/send、message/stream、tasks/get、tasks/cancel）；GET = Agent Card 发现。
        // 门禁与 ACP 同口径：key 直连先解析直连会话身份（会话归属需要 external_user.id），再按应用归属团队 + 已发布放行
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
                    tokenContext.AccessAppId,
                    routeAppId,
                    KeyDirectMarker(tokenContext),
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
        // 应用接入 key 以「直连会话身份」外部用户承载（资源 key 被包围圈拦截，到不了会话面）
        if (tokenContext!.IsKeyDirect && IsConversationPath(path))
        {
            var external = await _externalAccessKeyService.EnsurePrincipalUserAsync(
                (int)tokenContext.TeamId,
                tokenContext.AccessAppId,
                routeAppId,
                KeyDirectMarker(tokenContext),
                context.RequestAborted);

            tokenContext = WithExternalUser(tokenContext, external.Id, external.ExternalUserId);
            context.Items[ExternalAuthDefaults.TokenContextItemKey] = tokenContext;
            context.User = BuildPrincipal(tokenContext);
        }

        await next(context);
    }

    /// <summary>
    /// 提取 key 直连凭证：Bearer 值或 x-api-key 以应用接入 key（moai-ac-）或团队资源 key（moai-tr-）前缀开头时按 key 处理，外部 JWT 不会命中.
    /// </summary>
    private static string? ExtractKeyCredential(HttpContext context, string? bearer)
    {
        foreach (var candidate in new[] { bearer, context.Request.Headers["x-api-key"].ToString().Trim() })
        {
            if (string.IsNullOrEmpty(candidate))
            {
                continue;
            }

            if (candidate.StartsWith(ExternalAuthDefaults.AccessAppKeyPrefix, StringComparison.Ordinal)
                || candidate.StartsWith(ExternalAuthDefaults.ResourceKeyPrefix, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// 团队资源 key 路径包围圈：应用 key 仅 ACP/A2A 端点（端点级开关由各自门禁校验）；
    /// 知识库/知识图谱 key 仅各自资源前缀下的端点（路由资源 id 须等于绑定 id，且绑定资源已开启 MCP），
    /// 团队级列表端点（POST /api/teamres/wiki/list 等）放行、由 handler 按绑定资源过滤.
    /// </summary>
    private static async Task<bool> IsResourceKeyAllowedAsync(HttpContext context, PathString path, ExternalTokenContext tokenContext)
    {
        switch (tokenContext.ResourceKeyType)
        {
            case TeamResourceType.App:
                if ((!IsAppAcpPath(path) && !IsAppA2aPath(path)) || !TryGetRouteId(context, "appId", out var boundAppId) || boundAppId != tokenContext.ResourceKeyId)
                {
                    await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "insufficient_scope",
                        "The resource key only grants access to its bound resource endpoints.");
                    return false;
                }

                return true;
            case TeamResourceType.Wiki:
                if (!path.StartsWithSegments("/api/teamres/wiki", StringComparison.OrdinalIgnoreCase))
                {
                    await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "insufficient_scope",
                        "The resource key only grants access to its bound resource endpoints.");
                    return false;
                }

                var wikiIdMatched = TryGetRouteId(context, "wikiId", out var wikiId) && wikiId == tokenContext.ResourceKeyId;
                if (!wikiIdMatched && !IsResourceListPath(path, "/api/teamres/wiki/list"))
                {
                    await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "insufficient_scope",
                        "The resource key only grants access to its bound resource endpoints.");
                    return false;
                }

                return await EnsureResourceFeatureAsync(context, tokenContext);
            case TeamResourceType.KnowledgeGraph:
                if (!path.StartsWithSegments("/api/teamres/knowledge-graph", StringComparison.OrdinalIgnoreCase))
                {
                    await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "insufficient_scope",
                        "The resource key only grants access to its bound resource endpoints.");
                    return false;
                }

                var kgIdMatched = TryGetRouteId(context, "kgId", out var kgId) && kgId == tokenContext.ResourceKeyId;
                if (!kgIdMatched && !IsResourceListPath(path, "/api/teamres/knowledge-graph/list"))
                {
                    await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "insufficient_scope",
                        "The resource key only grants access to its bound resource endpoints.");
                    return false;
                }

                return await EnsureResourceFeatureAsync(context, tokenContext);
            default:
                await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "insufficient_scope",
                    "The resource key only grants access to its bound resource endpoints.");
                return false;
        }
    }

    /// <summary>
    /// 校验资源 key 绑定的知识库/知识图谱已开启 MCP（未开启时 key 不产生任何访问权限）.
    /// </summary>
    private static async Task<bool> EnsureResourceFeatureAsync(HttpContext context, ExternalTokenContext tokenContext)
    {
        var databaseContext = context.RequestServices.GetRequiredService<DatabaseContext>();
        var enabled = tokenContext.ResourceKeyType switch
        {
            TeamResourceType.Wiki => await databaseContext.Wikis
                .Where(x => x.Id.ToString() == tokenContext.ResourceKeyId)
                .Select(x => (bool?)x.IsMcpEnabled)
                .FirstOrDefaultAsync(context.RequestAborted),
            TeamResourceType.KnowledgeGraph => await databaseContext.KnowledgeGraphs
                .Where(x => x.Id.ToString() == tokenContext.ResourceKeyId)
                .Select(x => (bool?)x.IsMcpEnabled)
                .FirstOrDefaultAsync(context.RequestAborted),
            _ => true,
        };

        if (enabled != true)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "feature_disabled",
                "The feature of the bound resource is not enabled; enable it to use this resource key.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 资源 key 放行的团队级列表端点（精确匹配，结果由 handler 按绑定资源过滤）.
    /// </summary>
    private static bool IsResourceListPath(PathString path, string listPath)
    {
        return string.Equals(path.Value, listPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 读取路由中的资源 id（中间件注册在路由之后，控制器/Minimal API 路由值已可用）.
    /// </summary>
    private static bool TryGetRouteId(HttpContext context, string routeKey, out string id)
    {
        if (context.Request.RouteValues.TryGetValue(routeKey, out var value) && !string.IsNullOrEmpty(value?.ToString()))
        {
            id = value.ToString()!;
            return true;
        }

        id = string.Empty;
        return false;
    }

    private static bool IsConversationPath(PathString path)
    {
        return path.StartsWithSegments("/api/teamres/agent", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/teamres/session", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 是否为知识库 MCP 端点：/api/teamres/wiki/{wikiId}/mcp（外部 wiki 其余子路径不受影响）.
    /// </summary>
    private static bool IsWikiMcpPath(PathString path)
    {
        return path.StartsWithSegments("/api/teamres/wiki", StringComparison.OrdinalIgnoreCase)
            && path.Value!.EndsWith("/mcp", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 是否为知识图谱 MCP 端点：/api/teamres/knowledge-graph/{kgId}/mcp（外部知识图谱其余子路径不受影响）.
    /// </summary>
    private static bool IsKgMcpPath(PathString path)
    {
        return path.StartsWithSegments("/api/teamres/knowledge-graph", StringComparison.OrdinalIgnoreCase)
            && path.Value!.EndsWith("/mcp", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 是否为应用 ACP 端点：/api/teamres/app/{"{appId}"}/acp（app/list、access-point 等其余子路径不受影响）.
    /// </summary>
    private static bool IsAppAcpPath(PathString path)
    {
        return path.StartsWithSegments("/api/teamres/app", StringComparison.OrdinalIgnoreCase)
            && path.Value!.EndsWith("/acp", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 是否为应用 A2A 端点：/api/teamres/app/{"{appId}"}/a2a（JSON-RPC）与其 Agent Card 子路径 /a2a/agent.json.
    /// </summary>
    private static bool IsAppA2aPath(PathString path)
    {
        if (!path.StartsWithSegments("/api/teamres/app", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var value = path.Value!;
        return value.EndsWith("/a2a", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith("/a2a/agent.json", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// key 直连会话身份标识：团队资源 key 按行 id 派生（重置 key 值不变行 id，身份延续），其余用固定标识.
    /// </summary>
    private static string? KeyDirectMarker(ExternalTokenContext tokenContext)
    {
        return tokenContext.IsResourceKey ? $"__key_direct_{tokenContext.SubjectId}__" : null;
    }

    /// <summary>
    /// 应用 ACP 端点授权：凭证须带外部用户语义（用户 token 或已解析的 key 直连身份，承载会话归属），
    /// 路由 appId 存在、归属凭证团队（跨团队一律 404，不泄露存在性）且已发布可用.
    /// </summary>
    private static async Task<bool> IsAppAcpAllowedAsync(HttpContext context, Guid? appId, ExternalTokenContext tokenContext)
    {
        if (tokenContext.ExternalId <= 0)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "external_user_token_unsupported",
                "Application ACP only accepts an app access key (moai-ac-), a resource key (moai-tr-) or an external user token; sessions need an external user identity.");
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
            .Select(x => new { x.TeamId, x.IsDisable, x.PublishStatus, x.IsAcpA2aEnabled })
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

        if (!app.IsAcpA2aEnabled)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "feature_disabled",
                "Application ACP/A2A is not enabled; enable it on the app to use this endpoint.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 应用 A2A 端点授权：凭证须带外部用户语义（用户 token 或已解析的 key 直连身份，承载会话归属），
    /// 路由 appId 存在、归属凭证团队（跨团队一律 404，不泄露存在性）、已发布可用且应用已开启对外协议接入（与 ACP 同一开关）.
    /// </summary>
    private static async Task<bool> IsAppA2aAllowedAsync(HttpContext context, Guid? appId, ExternalTokenContext tokenContext)
    {
        if (tokenContext.ExternalId <= 0)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "external_user_token_unsupported",
                "Application A2A only accepts an app access key (moai-ac-), a resource key (moai-tr-) or an external user token; sessions need an external user identity.");
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
            .Select(x => new { x.TeamId, x.IsDisable, x.PublishStatus, x.IsAcpA2aEnabled })
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

        if (!app.IsAcpA2aEnabled)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "feature_disabled",
                "Application ACP/A2A is not enabled; enable it on the app to use this endpoint.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 知识图谱 MCP 端点授权：凭证主体须为接入 key / 应用 token / 资源 key（外部用户 token 无 MCP 语义），
    /// 路由 kgId 存在且归属凭证团队（跨团队一律 404，不泄露存在性）.
    /// </summary>
    private static async Task<bool> IsKgMcpAllowedAsync(HttpContext context, ExternalTokenContext tokenContext)
    {
        if (tokenContext.SubjectType != UserType.ExternalApp)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "external_user_token_unsupported",
                "Knowledge graph MCP only accepts an access key (moai-ac-/moai-tr-) or an app token.");
            return false;
        }

        if (!context.Request.RouteValues.TryGetValue("kgId", out var kgIdValue) || !long.TryParse(kgIdValue?.ToString(), out var kgId) || kgId <= 0)
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, "not_found_error", "kg_not_found", "Knowledge graph not found.");
            return false;
        }

        var databaseContext = context.RequestServices.GetRequiredService<DatabaseContext>();
        var kg = await databaseContext.KnowledgeGraphs
            .Where(x => x.Id == kgId)
            .Select(x => new { x.TeamId, x.IsMcpEnabled })
            .FirstOrDefaultAsync(context.RequestAborted);

        if (kg == null || (long)kg.TeamId != tokenContext.TeamId)
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, "not_found_error", "kg_not_found", "Knowledge graph not found.");
            return false;
        }

        if (!kg.IsMcpEnabled)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "feature_disabled",
                "Knowledge graph MCP is not enabled; enable it on the graph to use this endpoint.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 知识库 MCP 端点授权：凭证主体须为接入 key / 应用 token / 资源 key（外部用户 token 无 MCP 语义），
    /// 路由 wikiId 存在且归属凭证团队（跨团队一律 404，不泄露存在性）.
    /// </summary>
    private static async Task<bool> IsWikiMcpAllowedAsync(HttpContext context, ExternalTokenContext tokenContext)
    {
        if (tokenContext.SubjectType != UserType.ExternalApp)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "external_user_token_unsupported",
                "Knowledge base MCP only accepts an access key (moai-ac-/moai-tr-) or an app token.");
            return false;
        }

        if (!context.Request.RouteValues.TryGetValue("wikiId", out var wikiIdValue) || !long.TryParse(wikiIdValue?.ToString(), out var wikiId) || wikiId <= 0)
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, "not_found_error", "wiki_not_found", "Knowledge base not found.");
            return false;
        }

        var databaseContext = context.RequestServices.GetRequiredService<DatabaseContext>();
        var wiki = await databaseContext.Wikis
            .Where(x => x.Id == wikiId)
            .Select(x => new { x.TeamId, x.IsMcpEnabled })
            .FirstOrDefaultAsync(context.RequestAborted);

        if (wiki == null || (long)wiki.TeamId != tokenContext.TeamId)
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, "not_found_error", "wiki_not_found", "Knowledge base not found.");
            return false;
        }

        if (!wiki.IsMcpEnabled)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "feature_disabled",
                "Knowledge base MCP is not enabled; enable it on the wiki to use this endpoint.");
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
        var isAccessPointPath = context.Request.Path.StartsWithSegments("/api/teamres/app", StringComparison.OrdinalIgnoreCase)
            && context.Request.Path.Value!.EndsWith("/access-point", StringComparison.OrdinalIgnoreCase);
        if (tokenContext.ExternalId <= 0 && app.IsAuth && !isAccessPointPath)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_error", "external_user_token_required",
                "This application requires authorization. Exchange an external user token first via POST /api/teamres/token.");
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
            AppId = tokenContext.AppId,
            ExternalUserId = externalUserId,
            Nickname = tokenContext.Nickname,
            IsKeyDirect = tokenContext.IsKeyDirect,
            IsResourceKey = tokenContext.IsResourceKey,
            ResourceKeyType = tokenContext.ResourceKeyType,
            ResourceKeyId = tokenContext.ResourceKeyId,
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
