using Maomi.I18n;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using MoAI;
using MoAI.AI;
using MoAI.AI.A2a;
using MoAI.AI.Acp;
using MoAI.KnowledgeGraph;
using MoAI.KnowledgeGraph.Mcp;
using MoAI.Message.Hubs;
using MoAI.Wiki;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.UseMoAI();
builder.WebHost.ConfigureKestrel((options) =>
{
    options.Limits.MaxRequestBodySize = 1024 * 1024 * 1024; // 1GB

    // 内部
    options.ListenAnyIP(builder.Configuration.GetValue<int>("MoAI:Port"));

    // 外部应用、系统接口可以使用
    options.ListenAnyIP(builder.Configuration.GetValue<int>("MoAI:Port") + 1);
});

WebApplication app = builder.Build();

// 反向代理穿透：MoAI:ForwardedHeaders:Enabled=true 时从 X-Forwarded-For / X-Forwarded-Proto 解析真实客户端 IP，
// 登录风控、审计等依赖真实 IP 的能力必须开启；KnownProxies 为可信代理 IP 列表，留空表示信任任意直连对端
// （自托管部署通常在可信内网反代之后，代理 IP 不固定；公网直连暴露端口时切勿开启，否则 IP 可被伪造）。
// 必须置于管道最前，保证后续 CORS/认证/控制器读到的 RemoteIpAddress 已是真实客户端 IP
var forwardedHeadersConfig = builder.Configuration.GetSection("MoAI:ForwardedHeaders").Get<ForwardedHeadersOptions>();
if (forwardedHeadersConfig is { Enabled: true })
{
    var forwardOptions = new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions
    {
        ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
    };

    forwardOptions.KnownNetworks.Clear();
    forwardOptions.KnownProxies.Clear();
    if (forwardedHeadersConfig.KnownProxies != null)
    {
        foreach (var proxy in forwardedHeadersConfig.KnownProxies)
        {
            if (System.Net.IPAddress.TryParse(proxy, out var ip))
            {
                forwardOptions.KnownProxies.Add(ip);
            }
        }
    }

    app.UseForwardedHeaders(forwardOptions);
}

if (app.Environment.IsDevelopment())
{
    app.UseOpenApi(c =>
    {
        c.Path = "/openapi/{documentName}.json";
    });
    // API 文档：/scalar 同页下拉切换 v1（内部接口）、external（外部接口）与 subsys（子系统协作接口）三份文档，
    // 原始文档分别为 /openapi/v1.json、/openapi/external.json 与 /openapi/subsys.json（也可直接访问 /scalar/external、/scalar/subsys）
    app.MapScalarApiReference(options =>
    {
        options.AddDocument("v1", "AI API", isDefault: true);
        options.AddDocument("external", "AI External API");
        options.AddDocument("subsys", "AI Subsystem API");
    });
}
else
{
    // 部署环境：内部文档 v1 与 Scalar 不暴露，仅保留外部接口文档与子系统协作文档供接入方获取
    // （Path 不含 {documentName} 占位符时 DocumentName 为固定绑定，NSwag 不会响应其它文档名）
    app.UseOpenApi(c =>
    {
        c.Path = "/openapi/external.json";
        c.DocumentName = "external";
    });

    app.UseOpenApi(c =>
    {
        c.Path = "/openapi/subsys.json";
        c.DocumentName = "subsys";
    });

    // 其余 /openapi/**、/scalar/** 明确 404，避免落入 SPA 回退返回 index.html
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/openapi") ||
            context.Request.Path.StartsWithSegments("/scalar"))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next(context);
    });
}

// 私有网络访问（PNA）：浏览器对「公网/受限来源页面 → 本机服务」的请求会在预检中携带
// Access-Control-Request-Private-Network，要求响应显式带回 Allow 头才放行。外部悬浮组件
// 的宿主页（https 站点或 file://）访问本机部署的 MoAI 即此场景，必须在 UseCors 短路预检之前追加。
app.Use(async (context, next) =>
{
    if (context.Request.Headers.ContainsKey("Access-Control-Request-Private-Network"))
    {
        context.Response.Headers["Access-Control-Allow-Private-Network"] = "true";
    }

    await next(context);
});

app.UseCors("AllowSpecificOrigins");

// i18n：按请求语言（Accept-Language）解析文化，供 IStringLocalizer 翻译 BusinessException 等消息
app.UseI18n();

// 路由（显式声明，保证认证在端点分发之前执行）
app.UseRouting();

// 认证/授权：必须在 UseRouting 之后、任何解析 UserContext 的中间件/端点之前执行
app.UseAuthentication();
app.UseAuthorization();

// 自定义鉴权中间件
app.UseMiddleware<MoAI.App.ExternalAuthenticationMiddleware>();
app.UseMiddleware<MoAI.Subsystem.SubsysAccessMiddleware>();
app.UseMoAI();

// 配置静态文件服务（支持 SPA）
app.UseDefaultFiles();
app.UseStaticFiles();

// 静态资源中转：/static/{objectKey} => OSS（免登录、静态地址）
// 该中间件依赖的 StorageService 会解析 UserContext，必须置于 UseAuthentication 之后，
// 否则请求一开始就提前触发 GetUserContext() 并缓存匿名结果
app.UseMiddleware<MoAI.Storage.Middlewares.StorageStaticFilesMiddleware>();

#if DEBUG

#pragma warning disable CA1031 // 不捕获常规异常类型

app.Use(async (HttpContext context, RequestDelegate next) =>
{
    await Task.CompletedTask;
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    try
    {
        await next(context);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An unhandled exception occurred while processing the request.");
    }
});

#endif

app.UseHttpLogging();

// 知识库 MCP 服务器（/api/external/wiki/{wikiId}/mcp）：
// 接入 key 鉴权（含 wiki_mcp 范围与 wikiId 归属校验）由 ExternalAuthenticationMiddleware 在上方统一处理
app.MapWikiMcp();

// 知识图谱 MCP 服务器（/api/external/knowledge-graph/{kgId}/mcp）：鉴权（kg_mcp 范围与 kgId 归属）同由中间件统一处理；
// 与知识库 MCP 共享 McpServerOptions，工具按请求路径经 KnowledgeGraphMcpToolGate 分域（启动时注入一次 HttpContextAccessor）
KnowledgeGraphMcpToolGate.Initialize(app.Services.GetRequiredService<IHttpContextAccessor>());
app.MapKnowledgeGraphMcp();

app.MapControllers();

// 站内信/平台事件推送 Hub（/hub/moai）：只推事件不推数据，JWT 经 access_token 查询参数鉴权
// （见 ConfigureAuthorizaModule.OnMessageReceived），多实例经 Redis backplane 共享连接状态
app.MapHub<MoAIEventHub>("/hub/moai").RequireAuthorization();


// Agent 应用对话（AG-UI SSE 端点 /api/app/{appId}/chat）
app.MapAppAgentEndpoints();

// 应用 ACP 服务器（/api/external/app/{appId}/acp，agent-to-agent）：鉴权（app_acp 范围与 appId 归属）同由中间件统一处理
app.MapAppAcpEndpoint();

// 应用 A2A 服务器（/api/external/app/{appId}/a2a，Google Agent2Agent JSON-RPC）：鉴权（app_a2a 范围与 appId 归属）同由中间件统一处理
app.MapAppA2aEndpoint();

// SPA 回退：未匹配的路由返回 index.html（放在最后，以免抢在认证分发之前）
app.MapFallbackToFile("index.html");

app.Run();

/// <summary>
/// 反向代理穿透配置（MoAI:ForwardedHeaders 节）.
/// </summary>
public class ForwardedHeadersOptions
{
    /// <summary>
    /// 是否启用 X-Forwarded-* 解析（部署在反向代理之后时开启）.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// 可信代理 IP 列表；留空表示信任任意直连对端（可信内网部署）.
    /// </summary>
    public List<string>? KnownProxies { get; init; }
}
