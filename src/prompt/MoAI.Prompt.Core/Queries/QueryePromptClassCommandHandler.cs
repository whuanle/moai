// <copyright file="QueryePromptClassCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Prompt.Queries.Responses;

namespace MoAI.Prompt.Queries;

/// <summary>
/// <inheritdoc cref="QueryePromptClassCommand"/>
/// </summary>
public class QueryePromptClassCommandHandler : IRequestHandler<QueryePromptClassCommand, QueryePromptClassCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryePromptClassCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public QueryePromptClassCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryePromptClassCommandResponse> Handle(QueryePromptClassCommand request, CancellationToken cancellationToken)
    {
        var promptClass = await _databaseContext.PromptClasses
            .Select(x => new QueryePromptClassCommandResponseItem
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description
            })
            .ToArrayAsync(cancellationToken);

        return new QueryePromptClassCommandResponse { Items = promptClass };
    }
}
