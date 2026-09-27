using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MoAI;
using MoAI.AI;
using MoAI.AI.Acp;
using MoAI.Gateway;
using MoAI.KnowledgeGraph;
using MoAI.KnowledgeGraph.Mcp;
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

if (app.Environment.IsDevelopment())
{
    app.UseOpenApi(c =>
    {
        c.Path = "/openapi/{documentName}.json";
    });
    // API 文档：/scalar 查看内部接口（v1 文档），/scalar/external 查看外部接口（external 文档），
    // 原始文档分别为 /openapi/v1.json 与 /openapi/external.json
    app.MapScalarApiReference();
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

// 路由（显式声明，保证认证在端点分发之前执行）
app.UseRouting();

// 认证/授权：必须在 UseRouting 之后、任何解析 UserContext 的中间件/端点之前执行
app.UseAuthentication();
app.UseAuthorization();

// 自定义鉴权中间件
app.UseMiddleware<MoAI.App.ExternalAuthenticationMiddleware>();
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

// 团队模型网关（OpenAI/Anthropic 兼容 /v1 端点）
app.MapGatewayEndpoints();

// Agent 应用对话（AG-UI SSE 端点 /api/app/{appId}/chat）
app.MapAppAgentEndpoints();

// 应用 ACP 服务器（/api/external/app/{appId}/acp，agent-to-agent）：鉴权（app_acp 范围与 appId 归属）同由中间件统一处理
app.MapAppAcpEndpoint();

// SPA 回退：未匹配的路由返回 index.html（放在最后，以免抢在认证分发之前）
app.MapFallbackToFile("index.html");

app.Run();
