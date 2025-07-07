// <copyright file="Program.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using MoAI;
using MoAI.Infra.Models;
using Scalar.AspNetCore;
using System.Text.Json;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel((options) =>
{
    options.Limits.MaxRequestBodySize = 1024 * 1024 * 1024; // 1GB
});

builder.UseMaomiAI();

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

#pragma warning restore CA1031

#endif

app.UseDefaultExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

app.UseMaomiAI();

app.UseHttpLogging();

app.UseFastEndpoints((Action<Config>?)(c =>
{
    c.Endpoints.ShortNames = true;

    c.Endpoints.RoutePrefix = "api";
    c.Errors.ProducesMetadataType = typeof(BusinessValidationResult);

    // 拦截一些验证异常等
    c.Errors.ResponseBuilder = (failures, ctx, statusCode) =>
    {
        return (object)new MoAI.Infra.Models.BusinessValidationResult(failures, statusCode)
        {
            Detail = failures.FirstOrDefault()?.ErrorMessage ?? "请求参数错误",
            RequestId = ctx.TraceIdentifier,
        };
    };
    c.Serializer.Options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    c.Serializer.Options.Converters.Add(new System.Text.Json.LongStringConverter());
}));

app.MapControllers();

app.Run();