// <copyright file="QueryUserPluginBaseListCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Plugin.Models;
using MoAI.Plugin.Queries;
using MoAI.Plugin.Queries.Responses;
using MoAI.User.Queries;

namespace MaomiAI.Plugin.Core.Queries;

/// <summary>
/// <inheritdoc cref="QueryUserPluginBaseListCommand"/>
/// </summary>
public class QueryUserPluginBaseListCommandHandler : IRequestHandler<QueryUserPluginBaseListCommand, QueryPluginBaseListCommandResponse>
{
    private readonly DatabaseContext _dbContext;
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryUserPluginBaseListCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public QueryUserPluginBaseListCommandHandler(DatabaseContext dbContext, IMediator mediator, UserContext userContext)
    {
        _dbContext = dbContext;
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public async Task<QueryPluginBaseListCommandResponse> Handle(QueryUserPluginBaseListCommand request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Plugins.Where(x => x.IsSystem == false);

        if (!string.IsNullOrEmpty(request.Name))
        {
            query = query.Where(x => x.PluginName.Contains(request.Name));
        }

        if (request.Type.HasValue)
        {
            query = query.Where(x => x.Type == (int)request.Type.Value);
        }

        if (request.IsOwn == true)
        {
            query = query.Where(x => x.CreateUserId == _userContext.UserId);
        }

        var plugins = await query
            .Select(x => new PluginBaseInfoItem
            {
                PluginId = x.Id,
                Server = x.Server,
                PluginName = x.PluginName,
                Title = x.Title,
                OpenapiFileId = x.OpenapiFileId,
                OpenapiFileName = x.OpenapiFileName,
                Type = (PluginType)x.Type,
                Description = x.Description,
                CreateTime = x.CreateTime,
                CreateUserId = x.CreateUserId,
                UpdateTime = x.UpdateTime,
                UpdateUserId = x.UpdateUserId,
                IsSystem = x.IsSystem,
                IsPublic = x.IsPublic
            }).ToArrayAsync();

        await _mediator.Send(new FillUserInfoCommand { Items = plugins });

        return new QueryPluginBaseListCommandResponse { Items = plugins };
    }
}
/// <summary>
/// <inheritdoc cref="QuerySystemPluginBaseListCommand"/>
/// </summary>
public class QuerySystemPluginBaseListCommandHandler : IRequestHandler<QuerySystemPluginBaseListCommand, QueryPluginBaseListCommandResponse>
{
    private readonly DatabaseContext _dbContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuerySystemPluginBaseListCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="mediator"></param>
    public QuerySystemPluginBaseListCommandHandler(DatabaseContext dbContext, IMediator mediator)
    {
        _dbContext = dbContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryPluginBaseListCommandResponse> Handle(QuerySystemPluginBaseListCommand request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Plugins.Where(x => x.IsSystem == true);

        if (!string.IsNullOrEmpty(request.Name))
        {
            query = query.Where(x => x.PluginName.Contains(request.Name));
        }

        if (request.Type.HasValue)
        {
            query = query.Where(x => x.Type == (int)request.Type.Value);
        }

        var plugins = await query
            .Select(x => new PluginBaseInfoItem
            {
                PluginId = x.Id,
                Server = x.Server,
                PluginName = x.PluginName,
                Title = x.Title,
                OpenapiFileId = x.OpenapiFileId,
                OpenapiFileName = x.OpenapiFileName,
                Type = (PluginType)x.Type,
                Description = x.Description,
                CreateTime = x.CreateTime,
                CreateUserId = x.CreateUserId,
                UpdateTime = x.UpdateTime,
                UpdateUserId = x.UpdateUserId,
                IsSystem = x.IsSystem,
                IsPublic = x.IsPublic
            }).ToArrayAsync();

        await _mediator.Send(new FillUserInfoCommand { Items = plugins });

        return new QueryPluginBaseListCommandResponse { Items = plugins };
    }
}
