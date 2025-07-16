// <copyright file="QueryPluginDetailCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MaomiAI.Plugin.Core.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Plugin.Models;
using MoAI.Plugin.Queries.Responses;
using MoAI.User.Queries;

namespace MoAI.Plugin.Queries;

/// <summary>
/// <inheritdoc cref="QueryPluginDetailCommand"/>
/// </summary>
public class QueryPluginDetailCommandHandler : IRequestHandler<QueryPluginDetailCommand, QueryPluginDetailCommandResponse>
{
    private readonly DatabaseContext _dbContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPluginDetailCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="mediator"></param>
    public QueryPluginDetailCommandHandler(DatabaseContext dbContext, IMediator mediator)
    {
        _dbContext = dbContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryPluginDetailCommandResponse> Handle(QueryPluginDetailCommand request, CancellationToken cancellationToken)
    {
        var plugin = await _dbContext.Plugins.Where(x => x.Id == request.PluginId)
            .Select(x => new QueryPluginDetailCommandResponse
            {
                PluginId = x.Id,
                Server = x.Server,
                PluginName = x.PluginName,
                Title = x.Title,
                OpenapiFileId = x.OpenapiFileId,
                OpenapiFileName = x.OpenapiFileName,
                Header = TextToJsonExtensions.JsonToObject<IReadOnlyCollection<KeyValueString>>(x.Headers)!,
                Query = TextToJsonExtensions.JsonToObject<IReadOnlyCollection<KeyValueString>>(x.Queries)!,
                Type = (PluginType)x.Type,
                Description = x.Description,
                CreateTime = x.CreateTime,
                CreateUserId = x.CreateUserId,
                UpdateTime = x.UpdateTime,
                UpdateUserId = x.UpdateUserId,
                IsPublic = x.IsPublic
            }).FirstOrDefaultAsync();

        if (plugin == null)
        {
            throw new BusinessException("未找到或不能编辑此插件");
        }

        await _mediator.Send(new FillUserInfoCommand { Items = new QueryPluginDetailCommandResponse[] { plugin } });

        return plugin;
    }
}
