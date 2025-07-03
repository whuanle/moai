// <copyright file="ContentMiddleware.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

#pragma warning disable CA1031 // 不捕获常规异常类型

using Microsoft.AspNetCore.Http;
using MoAI.Infra.Models;
using MoAI.Infra.Service;

namespace MoAI.Storage;

/// <summary>
/// 拦截下载文件请求.
/// </summary>
public class ContentMiddleware : IMiddleware
{
    private readonly IAESProvider _aesProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentMiddleware"/> class.
    /// </summary>
    /// <param name="aesProvider"></param>
    public ContentMiddleware(IAESProvider aesProvider)
    {
        _aesProvider = aesProvider;
    }

    /// <inheritdoc/>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!context.Request.Path.StartsWithSegments("/contents", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        // /contents/{objectKey}?token={token}
        // token = {objectKey}/{timestamp}
        var objectKey = context.Request.RouteValues["key"]?.ToString();
        var token = context.Request.RouteValues["token"]?.ToString();

        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(objectKey))
        {
            await Status403(context);
            return;
        }

        // 验证签名
        string decryptKey = default!;

        try
        {
            decryptKey = _aesProvider.Decrypt(token);
        }
        catch (Exception ex)
        {
            _ = ex;
            await Status403(context);

            return;
        }

        var parts = decryptKey.Split('/');

        if (parts.Length > 1)
        {
            await Status403(context);

            return;
        }

        var objectKeyFromToken = parts[0];
        var timestamp = parts[1];

        if (objectKey != objectKeyFromToken)
        {
            await Status403(context);

            return;
        }

        var longTimestamp = long.TryParse(timestamp, out var ts) ? ts : 0;
        if (longTimestamp <= 0 || DateTimeOffset.Now.ToUnixTimeSeconds() - longTimestamp > 0)
        {
            await Status403(context);

            return;
        }

        await next(context);
    }

    private static async Task Status403(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;

        var error = new BusinessValidationResult
        {
            Code = 403,
            Detail = "Forbidden",
            RequestId = context.Request.HttpContext.TraceIdentifier,
        };

        await context.Response.WriteAsJsonAsync(error);
    }
}