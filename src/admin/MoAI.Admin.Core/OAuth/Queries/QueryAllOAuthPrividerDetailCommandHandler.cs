// <copyright file="QueryAllOAuthPrividerDetailCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Admin.OAuth.Queries.Responses;
using MoAI.Database;
using MoAI.Infra;
using MoAI.Login.Queries;
using MoAI.User.Queries;

namespace MoAI.Login.Querie;

/// <summary>
/// <inheritdoc cref="QueryAllOAuthPrividerDetailCommand"/>
/// </summary>
public class QueryAllOAuthPrividerDetailCommandHandler : IRequestHandler<QueryAllOAuthPrividerDetailCommand, QueryAllOAuthPrividerDetailCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAllOAuthPrividerDetailCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    /// <param name="systemOptions"></param>
    public QueryAllOAuthPrividerDetailCommandHandler(DatabaseContext databaseContext, IMediator mediator, SystemOptions systemOptions)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public async Task<QueryAllOAuthPrividerDetailCommandResponse> Handle(QueryAllOAuthPrividerDetailCommand request, CancellationToken cancellationToken)
    {
        var items = await _databaseContext.OauthConnections
            .Select(c => new QueryAllOAuthPrividerDetailCommandResponseItem
            {
                Name = c.Name,
                IconUrl = c.IconUrl,
                Provider = c.Provider,
                Key = c.Key,
                WellKnown = c.WellKnown,
                CreateTime = c.CreateTime,
                CreateUserId = c.CreateUserId,
                Id = c.Id,
                OAuthId = c.Uuid,
                UpdateTime = c.UpdateTime,
                UpdateUserId = c.UpdateUserId
            })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            item.RedirectUri = new Uri(new Uri(_systemOptions.WebUI), $"/oauth_login?state={item.OAuthId}").ToString();
        }

        await _mediator.Send(new FillUserInfoCommand { Items = items });

        return new QueryAllOAuthPrividerDetailCommandResponse { Items = items };
    }
}