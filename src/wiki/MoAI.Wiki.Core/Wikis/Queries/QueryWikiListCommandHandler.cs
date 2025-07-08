// <copyright file="QueryWikiListCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Wikis.Queries;

/// <summary>
/// 查询团队知识库列表.
/// </summary>
public class QueryWikiListCommandHandler : IRequestHandler<QueryWikiListCommand, IReadOnlyCollection<QueryWikiSimpleInfoResponse>>
{
    private readonly DatabaseContext _databaseContext;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="userContext"></param>
    public QueryWikiListCommandHandler(DatabaseContext databaseContext, UserContext userContext)
    {
        _databaseContext = databaseContext;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<QueryWikiSimpleInfoResponse>> Handle(QueryWikiListCommand request, CancellationToken cancellationToken)
    {
        var response = await _databaseContext.Wikis
            .Where(x => x.IsPublic || x.CreateUserId == _userContext.UserId || _databaseContext.WikiUsers.Any(a => a.WikiId == x.Id && a.UserId == _userContext.UserId))
            .OrderBy(x => x.Name)
            .Select(x => new QueryWikiSimpleInfoResponse
            {
                WikiId = x.Id,
                Name = x.Name,
                Description = x.Description,
                IsPublic = x.IsPublic,
            })
            .ToListAsync(cancellationToken);

        return response;
    }
}
