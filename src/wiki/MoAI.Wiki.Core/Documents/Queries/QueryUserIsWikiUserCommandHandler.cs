﻿// <copyright file="QueryUserIsWikiUserCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Wiki.Wikis.Queries;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Documents.Queries;

/// <summary>
/// <inheritdoc cref="QueryUserIsWikiUserCommand"/>
/// </summary>
public class QueryUserIsWikiUserCommandHandler : IRequestHandler<QueryUserIsWikiUserCommand, QueryUserIsWikiUserCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryUserIsWikiUserCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public QueryUserIsWikiUserCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    public async Task<QueryUserIsWikiUserCommandResponse> Handle(QueryUserIsWikiUserCommand request, CancellationToken cancellationToken)
    {
        var isWikiRoot = await _databaseContext.Wikis.Where(x => x.Id == request.WikiId && x.CreateUserId == request.UserId).AnyAsync(cancellationToken);
        var isWikiUser = await _databaseContext.WikiUsers.Where(x => x.WikiId == request.WikiId && x.UserId == request.UserId).AnyAsync(cancellationToken);
        var result = new QueryUserIsWikiUserCommandResponse
        {
            UserId = request.UserId,
            WikiId = request.WikiId,
            IsWikiRoot = isWikiRoot,
            IsWikiUser = isWikiUser || isWikiRoot
        };

        return result;
    }
}
