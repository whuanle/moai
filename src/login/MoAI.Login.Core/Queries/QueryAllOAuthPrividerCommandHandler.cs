// <copyright file="QueryAllOAuthPrividerCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra;
using MoAI.Login.Models;
using MoAI.Login.Queries;
using MoAI.Login.Queries.Responses;

namespace MoAI.Login.Querie;

/// <summary>
/// <inheritdoc cref="QueryAllOAuthPrividerCommand"/>
/// </summary>
public class QueryAllOAuthPrividerCommandHandler : IRequestHandler<QueryAllOAuthPrividerCommand, QueryAllOAuthPrividerCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAllOAuthPrividerCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="systemOptions"></param>
    public QueryAllOAuthPrividerCommandHandler(DatabaseContext databaseContext, SystemOptions systemOptions)
    {
        _databaseContext = databaseContext;
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public async Task<QueryAllOAuthPrividerCommandResponse> Handle(QueryAllOAuthPrividerCommand request, CancellationToken cancellationToken)
    {
        var items = await _databaseContext.OauthConnections
            .Where(c => c.IsDeleted == 0)
            .Select(c => new
            {
                Key = c.Key,
                OAuthId = c.Uuid,
                Name = c.Name,
                IconUrl = c.IconUrl,
                Provider = c.Provider,
                RedirectUrl = c.RedirectUri
            })
            .ToListAsync(cancellationToken);

        List<QueryAllOAuthPrividerCommandResponseItem> list = new();

        foreach (var item in items)
        {
            var frontUrl = _systemOptions.Server + $"/oauth_login";
            var redirectUrl = $"{item.RedirectUrl}?client_id={item.Key}&redirect_uri={frontUrl}&response_type=code&scope=openid%20profile&state={item.OAuthId}";

            if (OAuthPrivider.Feishu.ToString().Equals(item.Provider, StringComparison.OrdinalIgnoreCase))
            {
                // https://accounts.feishu.cn/open-apis/authen/v1/authorize?client_id=cli_a5d611352af9d00b&redirect_uri=https%3A%2F%2Fexample.com%2Fapi%2Foauth%2Fcallback&scope=bitable:app:readonly%20contact:contact&state=RANDOMSTRING
                redirectUrl = $"{item.RedirectUrl}?client_id={item.Key}&redirect_uri={frontUrl}&response_type=code&scope=&state={item.OAuthId}";
            }

            // var redirectUrl = $"{item.RedirectUrl}?client_id={item.Key}&response_type=code&scope=openid%20profile&state={item.OAuthId}";
            list.Add(new QueryAllOAuthPrividerCommandResponseItem
            {
                OAuthId = item.OAuthId,
                Name = item.Name,
                IconUrl = item.IconUrl,
                Provider = item.Provider,
                RedirectUrl = redirectUrl
            });
        }

        return new QueryAllOAuthPrividerCommandResponse { Items = list };
    }
}