// <copyright file="RefreshAdminsCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Models;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Login.Commands;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Login.Handlers;

public class RefreshAdminsCommandHandler : IRequestHandler<RefreshAdminsCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IRedisDatabase _redisDatabase;

    public RefreshAdminsCommandHandler(DatabaseContext databaseContext, IRedisDatabase redisDatabase = null)
    {
        _databaseContext = databaseContext;
        _redisDatabase = redisDatabase;
    }

    public async Task<EmptyCommandResponse> Handle(RefreshAdminsCommand request, CancellationToken cancellationToken)
    {
        await _redisDatabase.Database.KeyDeleteAsync("adminids");
        await _redisDatabase.Database.KeyDeleteAsync("rootid");

        var rootId = await _databaseContext.Settings.FirstOrDefaultAsync(x => x.Key == SystemSettingKeys.Root);

        if (rootId == null)
        {
            throw new BusinessException("系统未设置超级管理员.");
        }

        var adminIds = await _databaseContext.Users.Where(u => u.IsAdmin).Select(x => x.Id.ToString()).ToListAsync();

        adminIds.Add(rootId.Value);

        await _redisDatabase.SetAddAllAsync("adminids", StackExchange.Redis.CommandFlags.None, adminIds.ToArray());
        await _redisDatabase.Database.StringSetAsync("rootid", rootId.Value);

        return EmptyCommandResponse.Default;
    }
}
