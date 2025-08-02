// <copyright file="QuerySystemSettingsCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Admin.SystemSettings.Queries.Responses;
using MoAI.Database;
using MoAI.Database.Models;
using MoAI.User.Queries;

namespace MoAI.Admin.SystemSettings.Queries;

/// <summary>
/// <inheritdoc cref="QuerySystemSettingsCommand"/>
/// </summary>
public class QuerySystemSettingsCommandHandler : IRequestHandler<QuerySystemSettingsCommand, QuerySystemSettingsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuerySystemSettingsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QuerySystemSettingsCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QuerySystemSettingsCommandResponse> Handle(QuerySystemSettingsCommand request, CancellationToken cancellationToken)
    {
        List<QuerySystemSettingsCommandResponseItem> settings = new();

        var data = await _databaseContext.Settings.Select(x => new QuerySystemSettingsCommandResponseItem
        {
            Id = x.Id,
            Key = x.Key,
            Value = x.Value,
            Description = x.Description ?? string.Empty,
        }).ToArrayAsync();

        foreach (var item in ISystemSettingProvider.Keys.Where(x => x.IsEdit == true))
        {
            var result = data.FirstOrDefault(x => x.Key == item.Key);
            if (result != null)
            {
                settings.Add(result);
            }
            else
            {
                settings.Add(new QuerySystemSettingsCommandResponseItem
                {
                    Id = 0,
                    Key = item.Key,
                    Value = item.Value,
                    Description = item.Description,
                });
            }
        }

        return new QuerySystemSettingsCommandResponse { Items = settings };
    }
}