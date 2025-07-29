// <copyright file="QueryPromptListCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MaomiAI.Prompt.Models;
using MaomiAI.Prompt.Queries;
using MaomiAI.Prompt.Queries.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.User.Queries;

namespace MoAI.Prompt.Queries;

/// <summary>
/// <inheritdoc cref="QueryPromptListCommand"/>
/// </summary>
public class QueryPromptListCommandHandler : IRequestHandler<QueryPromptListCommand, QueryPromptListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPromptListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryPromptListCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryPromptListCommandResponse> Handle(QueryPromptListCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.Prompts.AsQueryable();

        if (request.IsOwn == true)
        {
            query = query.Where(x => x.CreateUserId == request.UserId);
        }
        else
        {
            query = query.Where(x => x.CreateUserId == request.UserId || x.IsPublic);
        }

        if (request.ClassId != null)
        {
            query = query.Where(x => x.PromptClassId == request.ClassId);
        }

        if (!string.IsNullOrEmpty(request.Name))
        {
            query = query.Where(x => x.Name.Contains(request.Name));
        }

        var prompts = await query
            .Select(x => new PromptItem
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                CreateTime = x.CreateTime,
                UpdateTime = x.UpdateTime,
                CreateUserId = x.CreateUserId,
                UpdateUserId = x.UpdateUserId,
                IsPublic = x.IsPublic,
                Content = string.Empty,
                PromptClassId = x.CreateUserId
            })
            .ToArrayAsync(cancellationToken);

        await _mediator.Send(new FillUserInfoCommand
        {
            Items = prompts
        });

        return new QueryPromptListCommandResponse
        {
            Items = prompts
        };
    }
}
