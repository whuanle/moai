// <copyright file="QuerySystemSettingsCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Admin.SystemSettings.Commands;
using MoAI.Database;
using MoAI.User.Queries;

namespace MoAI.Admin.SystemSettings.Queries;

public class QuerySystemSettingsCommandHandler : IRequestHandler<QuerySystemSettingsCommand, QuerySystemSettingsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    public QuerySystemSettingsCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    public async Task<QuerySystemSettingsCommandResponse> Handle(QuerySystemSettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = await _databaseContext.Settings.Select(x => new QuerySystemSettingsCommandResponseItem
        {
            Id = x.Id,
            Key = x.Key,
            Value = x.Value,
            Description = x.Description ?? string.Empty,
            CreateTime = x.CreateTime,
            CreateUserId = x.CreateUserId,
            UpdateTime = x.UpdateTime,
            UpdateUserId = x.UpdateUserId,
        }).ToArrayAsync();

        await _mediator.Send(new FillUserInfoCommand { Items = settings });

        return new QuerySystemSettingsCommandResponse { Settings = settings };
    }
}