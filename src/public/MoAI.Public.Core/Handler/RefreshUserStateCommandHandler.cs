// <copyright file="RefreshUserStateCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MoAI;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.Login.Commands;
using MoAI.Login.Queries.Responses;
using MoAI.Store.Queries;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Login.Handlers;

/// <summary>
/// <inheritdoc cref="RefreshUserStateCommand"/>
/// </summary>
public class RefreshUserStateCommandHandler : IRequestHandler<RefreshUserStateCommand, UserStateInfo>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IRedisDatabase _redisDatabase;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshUserStateCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="redisDatabase"></param>
    /// <param name="mediator"></param>
    public RefreshUserStateCommandHandler(DatabaseContext databaseContext, IRedisDatabase redisDatabase, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _redisDatabase = redisDatabase;
        _mediator = mediator;
    }

    public async Task<UserStateInfo> Handle(RefreshUserStateCommand request, CancellationToken cancellationToken)
    {
        UserStateInfo userCache = default!;

        var user = await _databaseContext.Users.Where(u => u.Id == request.UserId).FirstOrDefaultAsync(cancellationToken);

        if (user == null)
        {
            userCache = new UserStateInfo
            {
                IsDeleted = true,
                IsDisable = true,
                UserId = request.UserId
            };
        }
        else
        {
            var avatarUrl = string.Empty;
            if (!string.IsNullOrEmpty(user.AvatarPath))
            {
                var fileUrls = await _mediator.Send(new QueryPublicFileUrlFromKeyCommand { ObjectKeys = new List<string>() { user.AvatarPath } });
                avatarUrl = fileUrls.Urls.FirstOrDefault().Value?.ToString() ?? string.Empty;
            }
            else
            {
                avatarUrl = string.Empty;
            }

            userCache = new UserStateInfo
            {
                UserId = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                NickName = user.NickName,
                AvatarPath = avatarUrl,
                Phone = user.Phone,
                IsDisable = user.IsDisable,
                IsAdmin = user.IsAdmin,
                IsDeleted = user.IsDeleted > 0,
            };
        }

        var key = $"userstate:{request.UserId}";

        await _redisDatabase.Database.KeyDeleteAsync(key);
        await _redisDatabase.Database.StringSetAsync(key, userCache.ToRedisValue());
        await _redisDatabase.Database.KeyExpireAsync(key, TimeSpan.FromHours(1));

        return userCache;
    }
}
