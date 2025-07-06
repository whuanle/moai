// <copyright file="ApplicationBuilderExtensions.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using MoAI.Infra;

namespace MoAI.Storage.Extensions;

public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// 配置使用本地文件.
    /// </summary>
    /// <param name="builder"></param>
    public static void UseLocalFiles(this IApplicationBuilder builder)
    {
        File.WriteAllText("D:/aaa.txt", contents: DateTimeOffset.Now.ToString());

        var systemOptions = builder.ApplicationServices.GetRequiredService<SystemOptions>();

        var filePath = Path.Combine(systemOptions.FilePath, "files");
        var contentPath = Path.Combine(systemOptions.FilePath, "contents");

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
