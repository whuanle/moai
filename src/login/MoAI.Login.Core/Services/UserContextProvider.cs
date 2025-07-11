// <copyright file="UserContextProvider.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Microsoft.AspNetCore.Http;
using MoAI.Infra.Defaults;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace MoAI.Login.Services;

/// <summary>
/// 用户上下文提供者.
/// </summary>
public class UserContextProvider : IUserContextProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserContextProvider"/> class.
    /// </summary>
    /// <param name="httpContextAccessor"></param>
    public UserContextProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;

        _userContext = new Lazy<UserContext>(() =>
        {
            return Parse();
        });
    }

    private readonly Lazy<UserContext> _userContext;

    /// <inheritdoc/>
    public UserContext GetUserContext() => _userContext.Value;

    private DefaultUserContext Parse()
    {
        var httpContext = _httpContextAccessor.HttpContext;

        var user = httpContext?.User;
        if (httpContext == null || user == null || user.Identity == null || !user.Identity.IsAuthenticated)
        {
            return new DefaultUserContext
            {
                IsAuthenticated = false,
                UserId = 0,
                UserName = "Anonymous",
                NickName = "Anonymous",
                Email = string.Empty
            };
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = user.FindFirstValue(JwtRegisteredClaimNames.Name);
        var nickName = user.FindFirstValue(JwtRegisteredClaimNames.Nickname);
        var email = user.FindFirstValue(ClaimTypes.Email);

        return new DefaultUserContext
        {
            IsAuthenticated = user.Identity.IsAuthenticated,
            UserId = int.TryParse(userId, out var guid) ? guid : throw new BusinessException("Token 格式错误") { StatusCode = 401 },
            UserName = userName ?? string.Empty,
            NickName = nickName ?? string.Empty,
            Email = email ?? string.Empty
        };
    }
}
