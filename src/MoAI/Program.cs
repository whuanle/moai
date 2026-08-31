using MoAI;
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
    app.MapScalarApiReference();
}

app.UseCors("AllowSpecificOrigins");

// 路由（显式声明，保证认证在端点分发之前执行）
app.UseRouting();

// 认证/授权：必须在 UseRouting 之后、任何解析 UserContext 的中间件/端点之前执行
app.UseAuthentication();
app.UseAuthorization();

// 自定义鉴权中间件
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

//// MCP 服务器，需要放在授权之前
//app.MapMcp("/mcp/wiki/{wikiId}");

app.MapControllers();

// SPA 回退：未匹配的路由返回 index.html（放在最后，以免抢在认证分发之前）
app.MapFallbackToFile("index.html");

app.Run();
