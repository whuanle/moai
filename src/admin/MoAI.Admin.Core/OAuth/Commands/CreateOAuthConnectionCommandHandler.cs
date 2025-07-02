// <copyright file="CreateOAuthConnectionCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Login.Commands;
using MoAI.Login.Http;

namespace MoAI.Login.Handlers;

/// <summary>
/// <inheritdoc cref="CreateOAuthConnectionCommand"/>
/// </summary>
public class CreateOAuthConnectionCommandHandler : IRequestHandler<CreateOAuthConnectionCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IOAuthClient _authClient;
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateOAuthConnectionCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="authClient"></param>
    /// <param name="systemOptions"></param>
    public CreateOAuthConnectionCommandHandler(DatabaseContext databaseContext, IOAuthClient authClient, SystemOptions systemOptions)
    {
        _databaseContext = databaseContext;
        _authClient = authClient;
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(CreateOAuthConnectionCommand request, CancellationToken cancellationToken)
    {
        var exist = await _databaseContext.OauthConnections
            .AnyAsync(x => x.Name == request.Name, cancellationToken);

        if (exist)
        {
            throw new BusinessException("认证名称已存在，请更换后重试.");
        }

        var oauthRedirectUrl = await GetRedirectUrl(request.WellKnown);

        var connection = new Database.Entities.OauthConnectionEntity
        {
            Uuid = Guid.NewGuid().ToString("N"),
            Name = request.Name,
            Provider = request.Provider,
            Key = request.Key,
            Secret = request.Secret,
            IconUrl = request.IconUrl?.ToString() ?? string.Empty,
            WellKnown = request.WellKnown.ToString()
        };

        // 请求端口，获取重定向地址

        /*
         https://<HOST>/login/oauth/authorize?
        client_id=CLIENT_ID&
        redirect_uri=REDIRECT_URI&
        response_type=code&
        scope=openid&
        state=STATE
         */

        // 对方回调示例 http://localhost:4000/aaaaaa?a=1&code=545b56c8be398326a78b&state=ABCD
        //var frontUrl = _systemOptions.Server + $"/oauth_login";
        //var redirectUrl = $"{oauthRedirectUrl}?client_id={connection.Key}&redirect_uri={frontUrl}&response_type=code&scope=openid%20profile&state={connection.Uuid}";
        connection.RedirectUri = oauthRedirectUrl;

        _databaseContext.OauthConnections.Add(connection);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }

    private async Task<string> GetRedirectUrl(Uri wellKnownUrl)
    {
        // 获取端点信息
        _authClient.Client.BaseAddress = new Uri(wellKnownUrl.Host);
        var wellKnown = await _authClient.GetWellKnownAsync(wellKnownUrl.PathAndQuery.TrimStart('/'));

        return wellKnown.AuthorizationEndpoint;
    }
}
