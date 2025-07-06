// <copyright file="StorageCoreModule.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi;
using Microsoft.Extensions.DependencyInjection;

namespace MoAI.Storage;

/// <summary>
/// StorageCoreModule.
/// </summary>
[InjectModule<StorageLocalModule>]
[InjectModule<StorageApiModule>]
public class StorageCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        context.Services.AddScoped<ContentMiddleware>();
    }
}
