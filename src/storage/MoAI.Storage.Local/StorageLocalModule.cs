// <copyright file="StorageLocalModule.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Infra;
using MoAI.Store.Services;

namespace MoAI.Storage;

/// <summary>
/// StorageLocalModule.
/// </summary>
public class StorageLocalModule : IModule
{
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageLocalModule"/> class.
    /// </summary>
    /// <param name="systemOptions"></param>
    public StorageLocalModule(SystemOptions systemOptions)
    {
        _systemOptions = systemOptions!;
    }

    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        var filePath = Path.Combine(_systemOptions.FilePath, "public");
        var contentPath = Path.Combine(_systemOptions.FilePath, "private");

        if (!Directory.Exists(_systemOptions.FilePath))
        {
            Directory.CreateDirectory(_systemOptions.FilePath);
        }

        if (!Directory.Exists(filePath))
        {
            Directory.CreateDirectory(filePath);
        }

        if (!Directory.Exists(contentPath))
        {
            Directory.CreateDirectory(contentPath);
        }
    }
}
