// <copyright file="StorageLocalModule.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Infra;
using MoAI.Storage.Services;
using MoAI.Store.Services;

namespace MoAI.Storage;

/// <summary>
/// StorageLocalModule.
/// </summary>
public class StorageLocalModule : IModule
{
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageLocalModule"/> class.
    /// </summary>
    /// <param name="configuration">配置.</param>
    public StorageLocalModule(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        var systemOptions = context.Configuration.GetSection("MoAI").Get<SystemOptions>() ?? throw new InvalidOperationException("SystemOptions is not configured.");

        var filePath = Path.Combine(systemOptions.FilePath, "files");
        var contentPath = Path.Combine(systemOptions.FilePath, "contents");

        if (!Directory.Exists(systemOptions.FilePath))
        {
            Directory.CreateDirectory(systemOptions.FilePath);
        }

        if (!Directory.Exists(filePath))
        {
            Directory.CreateDirectory(filePath);
        }

        if (!Directory.Exists(contentPath))
        {
            Directory.CreateDirectory(contentPath);
        }

        context.Services.AddScoped<IPrivateFileStorage, LocalPrivateStorage>();
        context.Services.AddScoped<IPublicFileStorage, LocalPubliceStorage>();
    }
}
