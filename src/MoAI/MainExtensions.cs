// <copyright file="MainExtensions.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi;
using MoAI.Infra;
using MoAI.Storage.Core.Extensions;
using Serilog;

namespace MaomiAI;

public static class MainExtensions
{
    public static IHostApplicationBuilder UseMaomiAI(this IHostApplicationBuilder builder)
    {
        // 不存在时，使用默认日志文件配置，剩下的由 MoAI.Infra.Configuration 导入配置
        if (!Directory.Exists(AppConst.ConfigsPath))
        {
            var loggerConfigPath = Path.Combine(AppConst.ConfigsTemplate, "logger.json");
            if (File.Exists(loggerConfigPath))
            {
                builder.Configuration.AddJsonFile(loggerConfigPath);
            }
        }

        builder.Services.AddSingleton<IConfigurationManager>(builder.Configuration);
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog((services, configuration) =>
        {
            configuration.ReadFrom.Services(services);
            configuration.ReadFrom.Configuration(builder.Configuration);
        });

        builder.Services.AddModule<MainModule>();

        builder.Logging.ClearProviders();
        builder.Services.AddSerilog((services, configuration) =>
        {
            configuration.ReadFrom.Services(services);
            configuration.ReadFrom.Configuration(builder.Configuration);
        });

        return builder;
    }

    public static IApplicationBuilder UseMaomiAI(this IApplicationBuilder builder)
    {
        // 使用认证中间件
        // app.UseMiddleware<CustomAuthorizaMiddleware>();
        var systemOptions = builder.ApplicationServices.GetRequiredService<SystemOptions>();
        if ("local".Equals(systemOptions.Storage.Type, StringComparison.OrdinalIgnoreCase))
        {
            builder.UseLocalFiles(systemOptions);
        }

        return builder;
    }
}