// <copyright file="CustomAuthorizaMiddleware.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MoAI.Infra.Services;

namespace MoAI.Login.Services;

/// <summary>
/// 鉴权中间件.
/// </summary>
public class CustomAuthorizaMiddleware : IMiddleware
{
    private readonly IUserContextProvider _userContextProvider;
    private readonly ILogger<CustomAuthorizaMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomAuthorizaMiddleware"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="userContextProvider"></param>
    public CustomAuthorizaMiddleware(ILogger<CustomAuthorizaMiddleware> logger, IUserContextProvider userContextProvider)
    {
        _logger = logger;
        _userContextProvider = userContextProvider;
    }

    /// <inheritdoc/>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var userContext = _userContextProvider.GetUserContext();

        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() != null)
        {
            await next(context);
            return;
        }

        // todo: 后续添加权限验证逻辑
        var authorizeData = endpoint?.Metadata.GetOrderedMetadata<IAuthorizeData>() ?? Array.Empty<IAuthorizeData>();

        await next(context);
    }
}