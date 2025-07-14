// <copyright file="QueryWikiUsersCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Wikis.Queries;

/// <summary>
/// <inheritdoc cref="QueryWikiUsersCommand"/>
/// </summary>
public class QueryWikiUsersCommandHandler : IRequestHandler<QueryWikiUsersCommand, QueryWikiUsersCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiUsersCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryWikiUsersCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiUsersCommandResponse> Handle(QueryWikiUsersCommand request, CancellationToken cancellationToken)
    {
        var result = await _databaseContext.WikiUsers
            .Where(x => x.WikiId == request.WikiId)
            .Join(_databaseContext.Users, wikiUser => wikiUser.UserId, user => user.Id, (wikiUser, user) => new QueryWikiUsersCommandResponseItem
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                AvatarPath = user.AvatarPath ?? string.Empty,
                NickName = user.NickName ?? string.Empty,
            })
            .ToListAsync(cancellationToken);

        return new QueryWikiUsersCommandResponse { Users = result };
    }
}
