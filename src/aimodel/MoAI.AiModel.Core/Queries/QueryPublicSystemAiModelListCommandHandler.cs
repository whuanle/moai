// <copyright file="QueryPublicSystemAiModelListCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AiModel.Models;
using MoAI.AiModel.Queries.Respones;
using MoAI.Database;
using MoAI.Infra.Extensions;

namespace MoAI.AiModel.Queries;

/// <summary>
/// <inheritdoc cref="QueryPublicSystemAiModelListCommand"/>
/// </summary>
public class QueryPublicSystemAiModelListCommandHandler : IRequestHandler<QueryPublicSystemAiModelListCommand, QueryPublicSystemAiModelListCommandResponse>
{
    private readonly DatabaseContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPublicSystemAiModelListCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    public QueryPublicSystemAiModelListCommandHandler(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<QueryPublicSystemAiModelListCommandResponse> Handle(QueryPublicSystemAiModelListCommand request, CancellationToken cancellationToken)
    {
        var query = _dbContext.AiModels.Where(x => x.IsSystem == true && x.IsPublic);

        if (request.Provider != null)
        {
            query = query.Where(x => x.AiProvider == request.Provider.ToJsonString());
        }

        if (request.AiModelType != null)
        {
            query = query.Where(x => x.AiModelType == request.AiModelType.ToJsonString());
        }

        var list = await query
                .Select(x => new AiNotKeyEndpoint
                {
                    Id = x.Id,
                    Name = x.Name,
                    DeploymentName = x.DeploymentName,
                    Title = x.Title,
                    AiModelType = x.AiModelType.JsonToObject<AiModelType>(),
                    Provider = x.AiProvider.JsonToObject<AiProvider>(),
                    ContextWindowTokens = x.ContextWindowTokens,
                    Endpoint = x.Endpoint,
                    Abilities = new ModelAbilities
                    {
                        Files = x.Files,
                        FunctionCall = x.FunctionCall,
                        ImageOutput = x.ImageOutput,
                        Vision = x.IsVision,
                    },
                    MaxDimension = x.MaxDimension,
                    TextOutput = x.TextOutput
                }).ToArrayAsync(cancellationToken: cancellationToken);

        return new QueryPublicSystemAiModelListCommandResponse
        {
            AiModels = list
        };
    }
}
