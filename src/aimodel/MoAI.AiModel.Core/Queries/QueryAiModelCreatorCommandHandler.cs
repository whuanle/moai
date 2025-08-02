// <copyright file="QueryAiModelCreatorCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AiModel.Queries.Respones;
using MoAI.Database;

namespace MoAI.AiModel.Queries;

/// <summary>
/// <inheritdoc cref="QueryAiModelCreatorCommand"/>
/// </summary>
public class QueryAiModelCreatorCommandHandler : IRequestHandler<QueryAiModelCreatorCommand, QueryAiModelCreatorCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAiModelCreatorCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public QueryAiModelCreatorCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryAiModelCreatorCommandResponse> Handle(QueryAiModelCreatorCommand request, CancellationToken cancellationToken)
    {
        return await _databaseContext.AiModels.Where(x => x.Id == request.ModelId)
            .Select(x => new QueryAiModelCreatorCommandResponse
            {
                Exist = x.Id > 0,
                CreatorId = x.CreateUserId,
                IsSystem = x.IsSystem,
                IsPublic = x.IsPublic
            })
            .FirstOrDefaultAsync(cancellationToken) ?? new QueryAiModelCreatorCommandResponse();
    }
}
