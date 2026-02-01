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

// 配置静态文件服务（支持 SPA）
app.UseDefaultFiles();
app.UseStaticFiles();

// SPA 回退：未匹配的路由返回 index.html
app.MapFallbackToFile("index.html");

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

// MCP 服务器，需要放在授权之前
app.MapMcp("/mcp/wiki/{wikiId}");

app.UseAuthentication();
app.UseAuthorization();

app.UseMoAI();

app.MapControllers();

app.Run();