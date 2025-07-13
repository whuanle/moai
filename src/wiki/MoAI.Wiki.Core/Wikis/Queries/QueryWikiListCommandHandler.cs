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
/// 查询团队知识库列表.
/// </summary>
public class QueryWikiListCommandHandler : IRequestHandler<QueryWikiListCommand, IReadOnlyCollection<QueryWikiSimpleInfoResponse>>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryWikiListCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<QueryWikiSimpleInfoResponse>> Handle(QueryWikiListCommand request, CancellationToken cancellationToken)
    {
        var response = await _databaseContext.Wikis
            .Where(x => x.IsPublic || x.CreateUserId == request.UserId || _databaseContext.WikiUsers.Any(a => a.WikiId == x.Id && a.UserId == request.UserId))
            .OrderBy(x => x.Name)
            .Select(x => new QueryWikiSimpleInfoResponse
            {
                WikiId = x.Id,
                Name = x.Name,
                Description = x.Description,
                IsPublic = x.IsPublic,
                CreateUserId = x.CreateUserId,
                UpdateUserId = x.UpdateUserId,
                CreateTime = x.CreateTime,
                UpdateTime = x.UpdateTime,
                IsAdmin = x.CreateUserId == request.UserId || _databaseContext.WikiUsers.Any(a => a.UserId == request.UserId),
                DocumentCount = _databaseContext.WikiDocuments.Where(a => a.WikiId == x.Id).Count()
            })
            .ToListAsync(cancellationToken);

        await _mediator.Send(new FillUserInfoCommand { Items = response });

        return response;
    }
}
