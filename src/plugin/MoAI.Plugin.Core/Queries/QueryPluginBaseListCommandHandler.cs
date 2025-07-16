// <copyright file="QueryPluginBaseListCommandHandler.cs" company="MoAI">
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
/// <inheritdoc cref="QueryPluginBaseListCommand"/>
/// </summary>
public class QueryPluginBaseListCommandHandler : IRequestHandler<QueryPluginBaseListCommand, QueryPluginBaseListCommandResponse>
{
    private readonly DatabaseContext _dbContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPluginBaseListCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="mediator"></param>
    public QueryPluginBaseListCommandHandler(DatabaseContext dbContext, IMediator mediator)
    {
        _dbContext = dbContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryPluginBaseListCommandResponse> Handle(QueryPluginBaseListCommand request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Plugins.AsQueryable();
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
            query = query.Where(x => x.CreateUserId == request.UserId);
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
                IsPublic = x.IsPublic
            }).ToArrayAsync();

        await _mediator.Send(new FillUserInfoCommand { Items = plugins });

        return new QueryPluginBaseListCommandResponse { Items = plugins };
    }
}
