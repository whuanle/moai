// <copyright file="QueryWikiListCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.User.Queries;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Wikis.Queries;

/// <summary>
/// <inheritdoc cref="QueryWikiBaseListCommand"/>.
/// </summary>
public class QueryWikiBaseListCommandHandler : IRequestHandler<QueryWikiBaseListCommand, IReadOnlyCollection<QueryWikiInfoResponse>>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiBaseListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryWikiBaseListCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<QueryWikiInfoResponse>> Handle(QueryWikiBaseListCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.Wikis.AsQueryable();

        if (request.IsSystem)
        {
            query = query.Where(x => x.IsSystem);
        }

        if (request.IsOwn == true)
        {
            query = query.Where(x => x.CreateUserId == request.UserId);
        }
        else if (request.IsPublic == true)
        {
            query = query.Where(x => x.IsPublic);
        }
        else if (request.IsUser == true)
        {
            query = query.Where(x => x.CreateUserId == request.UserId || _databaseContext.WikiUsers.Any(a => a.WikiId == x.Id && a.UserId == request.UserId));
        }

        var response = await query
            .OrderBy(x => x.Name)
            .Select(x => new QueryWikiInfoResponse
            {
                WikiId = x.Id,
                Name = x.Name,
                Description = x.Description,
                IsPublic = x.IsPublic,
                CreateUserId = x.CreateUserId,
                UpdateUserId = x.UpdateUserId,
                CreateTime = x.CreateTime,
                UpdateTime = x.UpdateTime,
                IsUser = x.CreateUserId == request.UserId || _databaseContext.WikiUsers.Any(a => a.UserId == request.UserId),
                DocumentCount = _databaseContext.WikiDocuments.Where(a => a.WikiId == x.Id).Count()
            })
            .ToListAsync(cancellationToken);

        await _mediator.Send(new FillUserInfoCommand { Items = response });

        return response;
    }
}
