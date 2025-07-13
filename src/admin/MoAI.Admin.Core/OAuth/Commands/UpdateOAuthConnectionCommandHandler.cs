// <copyright file="UpdateOAuthConnectionCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Infra.OAuth;
using MoAI.Login.Commands;
using MoAI.Login.Models;

namespace MoAI.Login.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateOAuthConnectionCommand"/>
/// </summary>
public class UpdateOAuthConnectionCommandHandler : IRequestHandler<UpdateOAuthConnectionCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IOAuthClient _authClient;
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateOAuthConnectionCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="authClient"></param>
    /// <param name="systemOptions"></param>
    public UpdateOAuthConnectionCommandHandler(DatabaseContext databaseContext, IOAuthClient authClient, SystemOptions systemOptions)
    {
        _databaseContext = databaseContext;
        _authClient = authClient;
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateOAuthConnectionCommand request, CancellationToken cancellationToken)
    {
        var connection = await _databaseContext.OauthConnections
            .FirstOrDefaultAsync(x => x.Id == request.OAuthConnectionId, cancellationToken);

        if (connection == null)
        {
            throw new BusinessException("未找到认证方式，请检查名称是否正确.");
        }

        if (connection.Name != request.Name)
        {
            var exist = await _databaseContext.OauthConnections
                .AnyAsync(x => x.Id != request.OAuthConnectionId && x.Name == request.Name, cancellationToken);

            if (exist)
            {
                throw new BusinessException("认证名称已存在，请更换后重试.");
            }
        }

        connection.Key = request.Key;
        connection.IconUrl = request.IconUrl;

        if (!string.IsNullOrEmpty(request.Secret))
        {
            connection.Secret = request.Secret;
        }

        var sourceProvider = TextToJsonExtensions.JsonToObject<OAuthPrivider>(connection.Provider);

        if (sourceProvider == OAuthPrivider.Custom)
        {
            connection.WellKnown = request.WellKnown.ToString();

            var oauthRedirectUrl = await GetRedirectUrl(request.WellKnown);

            // var frontUrl = _systemOptions.Server + $"/oauth_login";
            // var redirectUrl = $"{oauthRedirectUrl}?client_id={connection.Key}&redirect_uri={frontUrl}&response_type=code&scope=openid%20profile&state={connection.Uuid}";
            connection.RedirectUri = oauthRedirectUrl;
        }
        else if (sourceProvider == OAuthPrivider.Feishu)
        {
            connection.RedirectUri = "https://accounts.feishu.cn/open-apis/authen/v1/authorize";
            connection.WellKnown = "https://accounts.feishu.cn";
        }

        _databaseContext.Update(connection);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }

    private async Task<string> GetRedirectUrl(Uri wellKnownUrl)
    {
        // 获取端点信息
        _authClient.Client.BaseAddress = new Uri(wellKnownUrl.GetLeftPart(UriPartial.Authority));
        var wellKnown = await _authClient.GetWellKnownAsync(wellKnownUrl.PathAndQuery.TrimStart('/'));

        return wellKnown.AuthorizationEndpoint;
    }
}