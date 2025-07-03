// <copyright file="OAuthBindExistAccountCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.OAuth;
using MoAI.Login.Commands;
using MoAI.Login.Models;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Login.Handlers;

/// <summary>
/// <inheritdoc cref="OAuthBindExistAccountCommand"/>
/// </summary>
public class OAuthBindExistAccountCommandHandler : IRequestHandler<OAuthBindExistAccountCommand, EmptyCommandResponse>
{
    private readonly UserContext _userContext;
    private readonly DatabaseContext _databaseContext;
    private readonly IOAuthClient _authClient;
    private readonly IOAuthClientAccessToken _authClientAccessToken;
    private readonly ILogger<OAuthBindExistAccountCommandHandler> _logger;
    private readonly IRedisDatabase _redisDatabase;

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthBindExistAccountCommandHandler"/> class.
    /// </summary>
    /// <param name="userContext"></param>
    /// <param name="databaseContext"></param>
    /// <param name="authClient"></param>
    /// <param name="authClientAccessToken"></param>
    /// <param name="logger"></param>
    /// <param name="redisDatabase"></param>
    public OAuthBindExistAccountCommandHandler(UserContext userContext, DatabaseContext databaseContext, IOAuthClient authClient, IOAuthClientAccessToken authClientAccessToken, ILogger<OAuthBindExistAccountCommandHandler> logger, IRedisDatabase redisDatabase)
    {
        _userContext = userContext;
        _databaseContext = databaseContext;
        _authClient = authClient;
        _authClientAccessToken = authClientAccessToken;
        _logger = logger;
        _redisDatabase = redisDatabase;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(OAuthBindExistAccountCommand request, CancellationToken cancellationToken)
    {
        // 绑定 OAuth 用户信息
        var redisKey = $"oauth:bind:{request.OAuthBindId}";
        var oauthBindUserProfile = await _redisDatabase.GetAsync<OAuthBindUserProfile>(redisKey);

        if (oauthBindUserProfile == null)
        {
            throw new BusinessException("第三方授权跳转登录已过期") { StatusCode = 403 };
        }

        var oauthConnectionEntity = await _databaseContext.OauthConnections
            .FirstOrDefaultAsync(c => c.Id == oauthBindUserProfile.OAuthId, cancellationToken);

        if (oauthConnectionEntity == null)
        {
            throw new BusinessException("未找到认证方式") { StatusCode = 404 };
        }

        var userEntity = await _databaseContext.Users.Where(x => _databaseContext.UserOauths.Any(a => a.UserId == x.Id && a.Id == oauthConnectionEntity.Id)).FirstOrDefaultAsync();

        if (userEntity != null)
        {
            if (userEntity.Id != _userContext.UserId)
            {
                throw new BusinessException("该账号已被绑定") { StatusCode = 400 };
            }

            // 已经绑定过，忽略后续操作
            return EmptyCommandResponse.Default;
        }

        // 绑定账号
        var oauthEntity = new UserOauthEntity
        {
            UserId =_userContext.UserId,
            ProviderId = oauthConnectionEntity.Id,
            Sub = oauthBindUserProfile.Profile.Sub,
        };

        await _databaseContext.UserOauths.AddAsync(oauthEntity, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
