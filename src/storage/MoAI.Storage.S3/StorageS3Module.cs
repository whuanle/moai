// <copyright file="StorageS3Module.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi;

namespace MoAI.Storage;

public class StorageS3Module : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        // var systemOptions = context.Configuration.Get<SystemOptions>();
        // if ("S3".Equals(systemOptions!.Storage.Type, StringComparison.OrdinalIgnoreCase))
        // {
        //    context.Services.AddScoped<IPrivateFileStorage, S3PrivateStorage>();
        //    context.Services.AddScoped<IPublicFileStorage, S3PublicStorage>();
        // }
    }
}
