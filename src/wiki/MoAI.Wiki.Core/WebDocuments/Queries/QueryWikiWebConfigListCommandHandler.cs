// <copyright file="QueryWikiWebConfigListCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.User.Queries;

namespace MoAI.Wiki.WebDocuments.Queries;

/// <summary>
/// <inheritdoc cref="QueryWikiWebConfigListCommand"/>
/// </summary>
public class QueryWikiWebConfigListCommandHandler : IRequestHandler<QueryWikiWebConfigListCommand, QueryWikiWebConfigListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiWebConfigListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryWikiWebConfigListCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiWebConfigListCommandResponse> Handle(QueryWikiWebConfigListCommand request, CancellationToken cancellationToken)
    {
        var configs = await _databaseContext.WikiWebConfigs
            .Where(x => x.WikiId == request.WikiId)
            .Select(x => new WeikiWebConfigSimpleItem
            {
                Id = x.Id,
                Title = x.Title,
                Address = x.Address,
                WikiId = x.WikiId,
                CreateTime = x.CreateTime,
                CreateUserId = x.CreateUserId,
                UpdateUserId = x.UpdateUserId,
                UpdateTime = x.UpdateTime,
            })
            .ToListAsync(cancellationToken);

        await _mediator.Send(new FillUserInfoCommand
        {
            Items = configs
        });

        return new QueryWikiWebConfigListCommandResponse
        {
            Items = configs
        };
    }
}
