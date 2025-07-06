// <copyright file="MainExtensions.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi;
using MoAI.Infra;
using MoAI.Login.Services;
using MoAI.Storage.Extensions;
using Serilog;
using System.Security.Cryptography;

namespace MoAI;

public static partial class MainExtensions
{
    public static IHostApplicationBuilder UseMaomiAI(this IHostApplicationBuilder builder)
    {
        if (!Directory.Exists(AppConst.ConfigsPath))
        {
            InitConfigurationDirectory();
        }

        ImportSystemConfiguration(builder);

        var systemOptions = builder.Configuration.GetSection("MoAI").Get<SystemOptions>() ?? throw new FormatException("The system configuration cannot be loaded.");

        builder.Services.AddSingleton(systemOptions);

        builder.Services.AddSingleton<IConfigurationManager>(builder.Configuration);

        builder.Logging.ClearProviders();
        builder.Services.AddSerilog((services, configuration) =>
        {
            configuration.ReadFrom.Services(services);
            configuration.ReadFrom.Configuration(builder.Configuration);
        });

        builder.Services.AddModule<MainModule>();

        return builder;
    }

    public static IApplicationBuilder UseMaomiAI(this IApplicationBuilder builder)
    {
        // 使用认证中间件
        builder.UseMiddleware<CustomAuthorizaMiddleware>();
        builder.UseLocalFiles();

        return builder;
    }
}