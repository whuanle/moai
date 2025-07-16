// <copyright file="QueryUserIsPluginCreatorCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Models;

namespace MoAI.Plugin.Queries;

/// <summary>
/// <inheritdoc cref="QueryUserIsPluginCreatorCommand"/>
/// </summary>
public class QueryUserIsPluginCreatorCommandHandler : IRequestHandler<QueryUserIsPluginCreatorCommand, SimpleBool>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryUserIsPluginCreatorCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public QueryUserIsPluginCreatorCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<SimpleBool> Handle(QueryUserIsPluginCreatorCommand request, CancellationToken cancellationToken)
    {
        var existPlugin = await _databaseContext.Plugins
            .AnyAsync(x => x.Id == request.PluginId && x.CreateUserId == request.UserId, cancellationToken);

        return new SimpleBool
        {
            Value = existPlugin
        };
    }
}
