// <copyright file="MaomiExceptionHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Microsoft.AspNetCore.Diagnostics;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.Filters;

/// <summary>
/// 异常拦截.<br />
/// </summary>
public class MaomiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<MaomiExceptionHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MaomiExceptionHandler"/> class.
    /// </summary>
    /// <param name="logger"></param>
    public MaomiExceptionHandler(ILogger<MaomiExceptionHandler> logger)
    {
        this.logger = logger;
    }

    /// <inheritdoc/>
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is BusinessException businessException)
        {
            HandleBusinessException(httpContext, businessException, cancellationToken);
        }
        else
        {
            ProcessUnhandledException(httpContext, exception, cancellationToken);
        }

        return ValueTask.FromResult(true);
    }

    private static void ProcessUnhandledException(HttpContext context, Exception ex, CancellationToken cancellationToken)
    {
        var message = string.Empty;
        var messageDetail = string.Empty;

#if DEBUG
        message = ex.Message;
#else
        Message = "Internal server error",
#endif

        var response = new BusinessValidationResult()
        {
            Code = 500,
            RequestId = context.TraceIdentifier,
            Detail = message,
        };

        context.Response.StatusCode = 500;

        context.Response.WriteAsJsonAsync(
            response,
            cancellationToken: cancellationToken);
    }

    private static void HandleBusinessException(HttpContext httpContext, BusinessException businessException, CancellationToken cancellationToken)
    {
        var message = string.Empty;
        var messageDetail = string.Empty;

        message = businessException.Message;
        if (businessException.Argments?.Count > 0)
        {
            message = string.Format(message, businessException.Argments.ToArray());
        }

#if DEBUG
        messageDetail = businessException.ToString();
#endif

        var response = new BusinessValidationResult()
        {
            Code = businessException.StatusCode,
            RequestId = httpContext.TraceIdentifier,
            Detail = message,
        };

        httpContext.Response.StatusCode = businessException.StatusCode;

        httpContext.Response.WriteAsJsonAsync(
            response,
            cancellationToken: cancellationToken);
    }
}