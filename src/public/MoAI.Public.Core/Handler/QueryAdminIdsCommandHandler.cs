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

public class QueryAdminIdsCommandHandler : IRequestHandler<QueryAdminIdsCommand, QueryAdminIdsCommandResponse>
{
    private readonly IRedisDatabase _redisDatabase;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAdminIdsCommandHandler"/> class.
    /// </summary>
    /// <param name="redisDatabase"></param>
    /// <param name="mediator"></param>
    public QueryAdminIdsCommandHandler(IRedisDatabase redisDatabase, IMediator mediator)
    {
        _redisDatabase = redisDatabase;
        _mediator = mediator;
    }

    public async Task<QueryAdminIdsCommandResponse> Handle(QueryAdminIdsCommand request, CancellationToken cancellationToken)
    {
        var existAdminIds = await _redisDatabase.Database.KeyExistsAsync("adminids");
        var existRootId = await _redisDatabase.Database.KeyExistsAsync("rootid");

        if (!existAdminIds || !existRootId)
        {
            await _mediator.Send(new RefreshAdminsCommand(), cancellationToken);
        }

        var adminIds = await _redisDatabase.SetMembersAsync<string>("adminids", StackExchange.Redis.CommandFlags.None);
        var rootId = await _redisDatabase.Database.StringGetAsync("rootid", StackExchange.Redis.CommandFlags.None);

        if (rootId.IsNullOrEmpty)
        {
            throw new BusinessException("系统未设置超级管理员.");
        }

        return new QueryAdminIdsCommandResponse
        {

            AdminIds = adminIds.Select(x => int.Parse(x)).ToList(),
            RootId = ((int)rootId)!
        };
    }
}