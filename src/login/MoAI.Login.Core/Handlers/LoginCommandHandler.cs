// <copyright file="LoginCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra;
using MoAI.Infra.Defaults;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Helpers;
using MoAI.Infra.Services;
using MoAI.Login.Commands;
using MoAI.Login.Commands.Responses;
using MoAI.Login.Models;
using MoAI.Login.Services;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Login.Handlers;

/// <summary>
/// 登录命令处理程序.
/// </summary>
public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginCommandResponse>
{
    private readonly DatabaseContext _dbContext;
    private readonly SystemOptions _systemOptions;
    private readonly IRedisDatabase _redisDatabase;
    private readonly IRsaProvider _rsaProvider;
    private readonly ITokenProvider _tokenProvider;
    private readonly ILogger<LoginCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="systemOptions"></param>
    /// <param name="redisDatabase"></param>
    /// <param name="rsaProvider"></param>
    /// <param name="tokenProvider"></param>
    /// <param name="logger"></param>
    public LoginCommandHandler(DatabaseContext dbContext, SystemOptions systemOptions, IRedisDatabase redisDatabase, IRsaProvider rsaProvider, ITokenProvider tokenProvider, ILogger<LoginCommandHandler> logger)
    {
        _dbContext = dbContext;
        _systemOptions = systemOptions;
        _redisDatabase = redisDatabase;
        _rsaProvider = rsaProvider;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    /// <summary>
    /// 处理登录命令.
    /// </summary>
    /// <param name="request">命令请求.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>登录结果.</returns>
    public async Task<LoginCommandResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var cacheKey = $"login:fail:{request.UserName}";
        var failCount = await _redisDatabase.GetAsync<int>(cacheKey);

        if (failCount >= 5)
        {
            throw new BusinessException("登录失败次数过多，请稍后再试.") { StatusCode = 403 };
        }

        var user = await _dbContext.Users.Where(u =>
                                  u.UserName == request.UserName || u.Email == request.UserName)
                              .FirstOrDefaultAsync(cancellationToken);

        if (user == null)
        {
            await IncrementLoginFailCountAsync(cacheKey);
            throw new BusinessException("用户名或密码错误") { StatusCode = 401 };
        }

        if (user.IsDisable)
        {
            throw new BusinessException("用户已被禁用") { StatusCode = 401 };
        }

        try
        {
            var password = _rsaProvider.Decrypt(request.Password);
            if (!PBKDF2Helper.VerifyHash(password, user.Password, user.PasswordSalt))
            {
                await IncrementLoginFailCountAsync(cacheKey);
                throw new BusinessException("用户名或密码错误") { StatusCode = 401 };
            }
        }
        catch (Exception ex) when (ex is not BusinessException)
        {
            await IncrementLoginFailCountAsync(cacheKey);
            throw new BusinessException("用户名或密码错误") { StatusCode = 401 };
        }

        // 登录成功，清除失败计数
        await _redisDatabase.Database.KeyDeleteAsync(cacheKey);

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

        if (!string.IsNullOrEmpty(request.OAuthBindId))
        {
            await BindOAuthAccount(request, user, result, cancellationToken);
        }

        return result;
    }

    private async Task BindOAuthAccount(LoginCommand request, UserEntity user, LoginCommandResponse result, CancellationToken cancellationToken)
    {
        // 绑定 OAuth 用户信息
        var redisKey = $"oauth:bind:{request.OAuthBindId}";
        var oauthBIndUserProfile = await _redisDatabase.GetAsync<OAuthBindUserProfile>(redisKey);

        if (oauthBIndUserProfile == null)
        {
            throw new BusinessException("检验第三方账号失败，请重新跳转登录.");
        }

        var oauthUser = await _dbContext.UserOauths
            .FirstOrDefaultAsync(o => o.Sub == oauthBIndUserProfile.Profile.Sub);

        if (oauthUser != null)
        {
            if (oauthUser.UserId != user.Id)
            {
                await _redisDatabase.Database.KeyDeleteAsync(redisKey);
                throw new BusinessException("第三方账号已绑定其它账号.");
            }

            // 重复绑定，不需要处理
            return;
        }

        var oauthConnectionEntity = await _dbContext.OauthConnections
            .FirstOrDefaultAsync(c => c.Id == oauthBIndUserProfile.OAuthId);

        if (oauthConnectionEntity == null)
        {
            throw new BusinessException("未找到对应的 OAuth 认证方式") { StatusCode = 404 };
        }

        var oauthEntity = new UserOauthEntity
        {
            UserId = user.Id,
            ProviderId = oauthConnectionEntity.Id,
            Sub = oauthBIndUserProfile.Profile.Sub,
        };

        await _dbContext.UserOauths.AddAsync(oauthEntity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _redisDatabase.Database.KeyDeleteAsync(redisKey);
    }

    /// <summary>
    /// 增加登录失败计数.
    /// </summary>
    /// <param name="cacheKey">缓存键.</param>
    /// <returns>异步任务.</returns>
    private async Task IncrementLoginFailCountAsync(string cacheKey)
    {
        var failCount = await _redisDatabase.Database.StringIncrementAsync(cacheKey);
        await _redisDatabase.Database.KeyExpireAsync(cacheKey, TimeSpan.FromMinutes(5));
    }
}