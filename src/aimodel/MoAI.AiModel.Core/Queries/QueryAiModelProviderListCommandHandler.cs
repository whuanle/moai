// <copyright file="QueryAiModelProviderListCommandHandler.cs" company="MoAI">
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
using System.Reflection;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace MoAI.AiModel.Queries;

/// <summary>
/// 查询 ai 供应商列表和模型数量.
/// </summary>
public class QueryAiModelProviderListCommandHandler : IRequestHandler<QueryAiModelProviderListCommand, QueryAiModelProviderListResponse>
{
    private readonly DatabaseContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAiModelProviderListCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    public QueryAiModelProviderListCommandHandler(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<QueryAiModelProviderListResponse> Handle(QueryAiModelProviderListCommand request, CancellationToken cancellationToken)
    {
        var providers = new List<QueryAiModelProviderCount>();

        var list = await _dbContext.AiModels
            .GroupBy(x => x.AiProvider)
            .Select(x => new QueryAiModelProviderCount
            {
                Provider = x.Key.JsonToObject<AiProvider>(),
                Count = x.Count()
            })
            .ToListAsync(cancellationToken);

        foreach (AiProvider item in Enum.GetValues<AiProvider>())
        {
            providers.Add(new QueryAiModelProviderCount
            {
                Provider = item,
                Count = list.FirstOrDefault(x => x.Provider == item)?.Count ?? 0
            });
        }

        return new QueryAiModelProviderListResponse
        {
            Providers = providers
        };
    }
}