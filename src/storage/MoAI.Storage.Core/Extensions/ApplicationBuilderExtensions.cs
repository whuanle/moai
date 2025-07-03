// <copyright file="ApplicationBuilderExtensions.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;
using MoAI.Infra;

namespace MoAI.Storage.Core.Extensions;

public static class ApplicationBuilderExtensions
{
    public static void UseLocalFiles(this IApplicationBuilder builder, SystemOptions systemOptions)
    {
        var filePath = Path.Combine(systemOptions.Storage.FilePath, "files");
        var contentPath = Path.Combine(systemOptions.Storage.FilePath, "contents");

        if (!Directory.Exists(systemOptions.Storage.FilePath))
        {
            Directory.CreateDirectory(systemOptions.Storage.FilePath);
        }

        if (!Directory.Exists(filePath))
        {
            Directory.CreateDirectory(filePath);
        }

        if (!Directory.Exists(contentPath))
        {
            Directory.CreateDirectory(contentPath);
        }

        // 登录后就能直接看到的文件
        builder.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(filePath),
            RequestPath = "/files",
        });

        builder.UseMiddleware<ContentMiddleware>();

        // 需要验证签名才能访问的文件
        builder.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(contentPath),
            RequestPath = "/contents",
        });
    }
}
