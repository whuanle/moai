// <copyright file="QueryUserBindAccountCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.User.Queries.Responses;

namespace MoAI.User.Queries;

/// <summary>
/// <inheritdoc cref="QueryUserBindAccountCommand"/>
/// </summary>
public class QueryUserBindAccountCommandHandler : IRequestHandler<QueryUserBindAccountCommand, QueryUserBindAccountCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryUserBindAccountCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public QueryUserBindAccountCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryUserBindAccountCommandResponse> Handle(QueryUserBindAccountCommand request, CancellationToken cancellationToken)
    {
        var result = await _databaseContext.UserOauths.Where(x => x.UserId == request.UserId)
            .Join(_databaseContext.OauthConnections, a => a.ProviderId, b => b.Id, (a, b) => new QueryUserBindAccountCommandResponseItem
            {
                BindId = a.Id,
                Name = b.Name,
                ProviderId = a.ProviderId,
                IconUrl = b.IconUrl,
            }).ToListAsync(cancellationToken);

        return new QueryUserBindAccountCommandResponse
        {
            Items = result
        };
    }
}
