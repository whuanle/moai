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
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Infra.OAuth;
using MoAI.Login.Commands;
using MoAI.Login.Models;

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
        var exist = await _databaseContext.OauthConnections.AnyAsync(x => x.Name == request.Name, cancellationToken);

        if (exist)
        {
            throw new BusinessException("认证名称已存在，请更换后重试.");
        }

        if (request.Provider == OAuthPrivider.Custom)
        {
            await AddCustomConnectionAsync(request, cancellationToken);
        }
        else if (request.Provider == OAuthPrivider.Feishu)
        {
            await AddFeishuConnectionAsync(request, cancellationToken);
        }
        else if (request.Provider == OAuthPrivider.DingTalk)
        {
            await AddDingTalkConnectionAsync(request, cancellationToken);
        }

        return EmptyCommandResponse.Default;
    }

    private async Task AddCustomConnectionAsync(CreateOAuthConnectionCommand request, CancellationToken cancellationToken)
    {
        var oauthRedirectUrl = await GetRedirectUrl(request.WellKnown);

        var connection = new Database.Entities.OauthConnectionEntity
        {
            Uuid = Guid.NewGuid().ToString("N"),
            Name = request.Name,
            Provider = TextToJsonExtensions.ToJsonString(request.Provider),
            Key = request.Key,
            Secret = request.Secret,
            IconUrl = request.IconUrl,
            WellKnown = request.WellKnown.ToString(),
        };

        // 请求端口，获取重定向地址

        // 对方回调示例 http://localhost:4000/aaaaaa?a=1&code=545b56c8be398326a78b&state=ABCD
        // var frontUrl = _systemOptions.Server + $"/oauth_login";
        // var redirectUrl = $"{oauthRedirectUrl}?client_id={connection.Key}&redirect_uri={frontUrl}&response_type=code&scope=openid%20profile&state={connection.Uuid}";
        connection.AuthorizeUrl = oauthRedirectUrl;

        _databaseContext.OauthConnections.Add(connection);
        await _databaseContext.SaveChangesAsync(cancellationToken);
    }

    private async Task AddFeishuConnectionAsync(CreateOAuthConnectionCommand request, CancellationToken cancellationToken)
    {
        var fsConnection = new Database.Entities.OauthConnectionEntity
        {
            Uuid = Guid.NewGuid().ToString("N"),
            Name = request.Name,
            Provider = TextToJsonExtensions.ToJsonString(request.Provider),
            Key = request.Key,
            Secret = request.Secret,
            IconUrl = request.IconUrl,
            AuthorizeUrl = "https://accounts.feishu.cn/open-apis/authen/v1/authorize",
            WellKnown = request.WellKnown.ToString(),
        };

        _databaseContext.OauthConnections.Add(fsConnection);
        await _databaseContext.SaveChangesAsync(cancellationToken);
    }

    private async Task AddDingTalkConnectionAsync(CreateOAuthConnectionCommand request, CancellationToken cancellationToken)
    {
        // https://login.dingtalk.com/oauth2/auth?redirect_uri=https%3A%2F%2Fwww.aaaaa.com%2Fa%2Fb&response_type=code&client_id=dingbbbbbbb&scope=openid corpid&state=dddd&prompt=consent
        var weixinWorkConnection = new Database.Entities.OauthConnectionEntity
        {
            Uuid = Guid.NewGuid().ToString("N"),
            Name = request.Name,
            Provider = TextToJsonExtensions.ToJsonString(request.Provider),
            Key = request.Key,
            Secret = request.Secret,
            IconUrl = request.IconUrl,
            AuthorizeUrl = "https://login.dingtalk.com/oauth2/auth",
            WellKnown = "https://login.dingtalk.com/oauth2/auth"
        };

        _databaseContext.OauthConnections.Add(weixinWorkConnection);
        await _databaseContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> GetRedirectUrl(Uri wellKnownUrl)
    {
        // 获取端点信息
        _authClient.Client.BaseAddress = new Uri(wellKnownUrl.GetLeftPart(UriPartial.Authority));
        var wellKnown = await _authClient.GetWellKnownAsync(wellKnownUrl.PathAndQuery.TrimStart('/'));

        return wellKnown.AuthorizationEndpoint;
    }
}
