// <copyright file="ConfigureMVCModule.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FluentValidation;
using Maomi;
using MoAI.Infra;
using MoAI.Storage;
using System.Reflection;

namespace MoAI.Modules;

/// <summary>
/// 配置 MVC .
/// </summary>
public class ConfigureMVCModule : IModule
{
    private readonly IConfiguration _configuration;
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigureMVCModule"/> class.
    /// </summary>
    /// <param name="configuration"></param>
    public ConfigureMVCModule(IConfiguration configuration)
    {
        _configuration = configuration;
        _systemOptions = configuration.Get<SystemOptions>()!;
    }

    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        var mvcBuilder = context.Services.AddControllers(o =>
        {
        });

        AddApplicationParts(mvcBuilder, context);

        context.Services.AddCors(options =>
        {
            options.AddPolicy(
                name: "AllowSpecificOrigins",
                policy =>
                              {
                                  policy.AllowAnyOrigin()
                                  .AllowAnyHeader()
                                  .AllowAnyMethod();
                              });
        });

        context.Services.AddValidatorsFromAssemblies(context.Modules.Select(x => x.Assembly).Distinct());

        // context.Services.AddFluentValidationAutoValidation();
    }

    private static void AddApplicationParts(IMvcBuilder builder, ServiceContext context)
    {
        foreach (var item in context.Modules)
        {
            if (item.Assembly.GetName().Name?.EndsWith(".Api", StringComparison.CurrentCultureIgnoreCase) == true)
            {
                builder.AddApplicationPart(item.Assembly);
            }
        }

        builder.AddApplicationPart(typeof(StorageLocalModule).Assembly);
    }
}