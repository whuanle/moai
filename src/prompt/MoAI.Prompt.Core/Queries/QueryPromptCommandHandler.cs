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
using MoAI.Infra.Exceptions;
using MoAI.User.Queries;

namespace MoAI.Prompt.Queries;

/// <summary>
/// <inheritdoc cref="QueryPromptCommand"/>
/// </summary>
public class QueryPromptCommandHandler : IRequestHandler<QueryPromptCommand, PromptItem>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPromptCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryPromptCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<PromptItem> Handle(QueryPromptCommand request, CancellationToken cancellationToken)
    {
        var prompt = await _databaseContext.Prompts
            .Where(x => x.Id == request.PromptId)
            .Select(x => new PromptItem
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                CreateTime = x.CreateTime,
                UpdateTime = x.UpdateTime,
                CreateUserId = x.CreateUserId,
                UpdateUserId = x.UpdateUserId,
                Content = x.Content
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (prompt == null)
        {
            throw new BusinessException("提示词不存在") { StatusCode = 404 };
        }

        return prompt;
    }
}