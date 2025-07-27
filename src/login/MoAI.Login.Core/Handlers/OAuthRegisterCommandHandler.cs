// <copyright file="OAuthRegisterCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Infra.Defaults;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Services;
using MoAI.Login.Commands;
using MoAI.Login.Commands.Responses;
using MoAI.Login.Models;
using MoAI.Login.Services;
using StackExchange.Redis.Extensions.Core.Abstractions;
using System.Transactions;

namespace MoAI.Login.Handlers;

/// <summary>
/// <inheritdoc cref="OAuthRegisterCommand"/>
/// </summary>
public class OAuthRegisterCommandHandler : IRequestHandler<OAuthRegisterCommand, LoginCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;
    private readonly IRsaProvider _rsaProvider;
    private readonly IRedisDatabase _redisDatabase;
    private readonly ITokenProvider _tokenProvider;
    private readonly ILogger<OAuthRegisterCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthRegisterCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    /// <param name="rsaProvider"></param>
    /// <param name="redisDatabase"></param>
    /// <param name="tokenProvider"></param>
    /// <param name="logger"></param>
    public OAuthRegisterCommandHandler(DatabaseContext databaseContext, IMediator mediator, IRsaProvider rsaProvider, IRedisDatabase redisDatabase, ITokenProvider tokenProvider, ILogger<OAuthRegisterCommandHandler> logger)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
        _rsaProvider = rsaProvider;
        _redisDatabase = redisDatabase;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<LoginCommandResponse> Handle(OAuthRegisterCommand request, CancellationToken cancellationToken)
    {
        // 绑定 OAuth 用户信息
        var redisKey = $"oauth:bind:{request.TempOAuthBindId}";
        var oauthBindUserProfile = await _redisDatabase.GetAsync<OAuthBindUserProfile>(redisKey);

        if (oauthBindUserProfile == null)
        {
            throw new BusinessException("第三方授权跳转登录已过期") { StatusCode = 403 };
        }

        var oauthConnectionEntity = await _databaseContext.OauthConnections.FirstOrDefaultAsync(c => c.Id == oauthBindUserProfile.OAuthId);

        if (oauthConnectionEntity == null)
        {
            throw new BusinessException("未找到对应的 OAuth 认证方式") { StatusCode = 404 };
        }

        var existingOpenIdUser = await _databaseContext.UserOauths.Where(u => u.ProviderId == oauthConnectionEntity.Id && u.Sub == oauthBindUserProfile.Profile.Sub).AnyAsync();

        if (existingOpenIdUser)
        {
            throw new BusinessException("该 OAuth 用户已被注册") { StatusCode = 409 };
        }

        using TransactionScope transactionScope = new TransactionScope(
            scopeOption: TransactionScopeOption.Required,
            asyncFlowOption: TransactionScopeAsyncFlowOption.Enabled,
            transactionOptions: new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted });

        var userName = "u" + Guid.CreateVersion7().ToString("N");
        var registerUserCommand = new RegisterUserCommand()
        {
            UserName = userName,
            Email = userName + "@moai.com",
            NickName = oauthBindUserProfile.Profile.PreferredUsername,
            Phone = "12345678900",
            Password = _rsaProvider.Encrypt(Guid.NewGuid().ToString("N"))
        };

        var userId = await _mediator.Send(registerUserCommand, cancellationToken);

        var user = await _databaseContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId.Value, cancellationToken);

        if (user == null)
        {
            throw new BusinessException("用户注册失败，请联系管理员") { StatusCode = 500 };
        }

        user.UserName = $"u{user.Id}";
        _databaseContext.Users.Update(user);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        await _databaseContext.UserOauths.AddAsync(new Database.Entities.UserOauthEntity
        {
            UserId = userId.Value,
            ProviderId = oauthConnectionEntity.Id,
            Sub = oauthBindUserProfile.Profile.Sub
        });

        await _databaseContext.SaveChangesAsync();

        transactionScope.Complete();

        var userContext = new DefaultUserContext
        {
            UserId = user.Id,
            UserName = user.UserName,
            NickName = user.NickName,
            Email = user.Email
        };

        var (accessToken, refreshToken) = _tokenProvider.GenerateTokens(userContext);

        var result = new LoginCommandResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            UserId = user.Id,
            UserName = user.UserName,
            ExpiresIn = DateTimeOffset.Now.AddMinutes(30).ToUnixTimeMilliseconds()
        };

        _logger.LogInformation("User login.{@Message}", new { user.Id, user.UserName, user.NickName });

        return result;
    }
}
