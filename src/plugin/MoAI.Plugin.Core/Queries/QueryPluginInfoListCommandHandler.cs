// <copyright file="QueryPluginInfoListCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Helper;
using MoAI.Infra.Models;
using MoAI.Plugin.Models;
using MoAI.Plugin.Queries;
using MoAI.Plugin.Queries.Responses;
using MoAI.User.Queries;

namespace MaomiAI.Plugin.Core.Queries;

/// <summary>
/// <inheritdoc cref="QueryPluginInfoListCommand"/>
/// </summary>
public class QueryPluginInfoListCommandHandler : IRequestHandler<QueryPluginInfoListCommand, QueryPluginInfoListCommandResponse>
{
    private readonly DatabaseContext _dbContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPluginInfoListCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="mediator"></param>
    public QueryPluginInfoListCommandHandler(DatabaseContext dbContext, IMediator mediator)
    {
        _dbContext = dbContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryPluginInfoListCommandResponse> Handle(QueryPluginInfoListCommand request, CancellationToken cancellationToken)
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

        if (request.PluginIds != null && request.PluginIds.Count > 0)
        {
            query = query.Where(x => request.PluginIds.Contains(x.Id));
        }

        var plugins = await query
            .Select(x => new QueryPluginInfoItem
            {
                PluginId = x.Id,
                PluginName = x.PluginName,
                Title = x.Title,
                Type = (PluginType)x.Type,
                Description = x.Description,
            }).ToArrayAsync();

        return new QueryPluginInfoListCommandResponse { Items = plugins };
    }
}
