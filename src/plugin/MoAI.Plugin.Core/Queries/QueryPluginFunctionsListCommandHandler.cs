// <copyright file="QueryPluginFunctionsListCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Plugin.Queries;
using MoAI.Plugin.Queries.Responses;

namespace MaomiAI.Plugin.Core.Queries;

/// <summary>
/// <inheritdoc cref="QueryPluginFunctionsListCommand"/>
/// </summary>
public class QueryPluginFunctionsListCommandHandler : IRequestHandler<QueryPluginFunctionsListCommand, QueryPluginFunctionsListCommandResponse>
{
    private readonly DatabaseContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPluginFunctionsListCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    public QueryPluginFunctionsListCommandHandler(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<QueryPluginFunctionsListCommandResponse> Handle(QueryPluginFunctionsListCommand request, CancellationToken cancellationToken)
    {
        var plugins = await _dbContext.PluginFunctions.Where(x => x.PluginId == request.PluginId)
            .Select(x => new QueryPluginFunctionsListCommandResponseItem
            {
                PluginId = x.PluginId,
                FunctionId = x.Id,
                Name = x.Name,
                Path = x.Path,
                Summary = x.Summary,
            }).ToArrayAsync();

        return new QueryPluginFunctionsListCommandResponse { Items = plugins };
    }
}