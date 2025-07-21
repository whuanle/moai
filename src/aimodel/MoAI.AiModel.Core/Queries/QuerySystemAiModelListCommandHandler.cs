// <copyright file="QueryAiModelListCommandHandler.cs" company="MoAI">
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
using MoAI.Infra.Models;

namespace MoAI.AiModel.Queries;

/// <summary>
/// 查询模型列表.
/// </summary>
public class QuerySystemAiModelListCommandHandler : IRequestHandler<QuerySystemAiModelListCommand, QuerySystemAiModelListCommandResponse>
{
    private readonly DatabaseContext _dbContext;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuerySystemAiModelListCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="userContext"></param>
    public QuerySystemAiModelListCommandHandler(DatabaseContext dbContext, UserContext userContext)
    {
        _dbContext = dbContext;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public async Task<QuerySystemAiModelListCommandResponse> Handle(QuerySystemAiModelListCommand request, CancellationToken cancellationToken)
    {
        var query = _dbContext.AiModels.Where(x => x.IsSystem == true);

        if (request.Provider != null)
        {
            query = query.Where(x => x.AiProvider == request.Provider.ToJsonString());
        }

        if (request.AiModelType != null)
        {
            query = query.Where(x => x.AiModelType == request.AiModelType.ToJsonString());
        }

        var list = await query
                .Select(x => new QuerySystemAiModelListCommandResponseItem
                {
                    Id = x.Id,
                    Name = x.Name,
                    DeploymentName = x.DeploymentName,
                    Title = x.Title,
                    AiModelType = x.AiModelType.JsonToObject<AiModelType>(),
                    Provider = x.AiProvider.JsonToObject<AiProvider>(),
                    ContextWindowTokens = x.ContextWindowTokens,
                    Endpoint = x.Endpoint,
                    IsSystem = x.IsSystem,
                    IsPublic = x.IsPublic,
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

        return new QuerySystemAiModelListCommandResponse
        {
            AiModels = list
        };
    }
}