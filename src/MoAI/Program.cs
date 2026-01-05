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

app.UseStaticFiles();

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

app.UseAuthentication();
app.UseAuthorization();

app.UseMoAI();

app.UseHttpLogging();

app.MapControllers();

app.Run();