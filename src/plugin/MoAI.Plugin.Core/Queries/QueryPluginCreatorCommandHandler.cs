// <copyright file="QueryUserIsPluginCreatorCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.Plugin.Queries.Responses;

namespace MoAI.Plugin.Queries;

/// <summary>
/// <inheritdoc cref="QueryPluginCreatorCommand"/>
/// </summary>
public class QueryPluginCreatorCommandHandler : IRequestHandler<QueryPluginCreatorCommand, QueryPluginCreatorCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPluginCreatorCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public QueryPluginCreatorCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryPluginCreatorCommandResponse> Handle(QueryPluginCreatorCommand request, CancellationToken cancellationToken)
    {
        var plugin = await _databaseContext.Plugins
            .Where(x => x.Id == request.PluginId)
            .Select(x => new QueryPluginCreatorCommandResponse
            {
                CreatorId = x.CreateUserId,
                PluginId = x.Id,
                IsSystem = x.IsSystem,
                Exist = x.Id > 0
            }).FirstOrDefaultAsync(cancellationToken);

        return plugin;
    }
}
