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

public class StorageLocalModule : IModule
{
    public void ConfigureServices(ServiceContext context)
    {
        var systemOptions = context.Configuration.Get<SystemOptions>();
        if ("local".Equals(systemOptions!.Storage.Type, StringComparison.OrdinalIgnoreCase))
        {
            context.Services.AddScoped<IPrivateFileStorage, LocalPrivateStorage>();
            context.Services.AddScoped<IPublicFileStorage, LocalPubliceStorage>();
        }
    }
}
