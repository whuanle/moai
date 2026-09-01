using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Infra.OAuth;
using MoAI.OauthConnect.Commands;

namespace MoAI.OauthConnect.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateOAuthConnectionCommand"/>
/// </summary>
public class UpdateOAuthConnectionCommandHandler : IRequestHandler<UpdateOAuthConnectionCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IOAuthClientFactory _authClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateOAuthConnectionCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="authClientFactory"></param>
    public UpdateOAuthConnectionCommandHandler(DatabaseContext databaseContext, IOAuthClientFactory authClientFactory)
    {
        _databaseContext = databaseContext;
        _authClientFactory = authClientFactory;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateOAuthConnectionCommand request, CancellationToken cancellationToken)
    {
        var connection = await _databaseContext.OauthConnections
            .FirstOrDefaultAsync(x => x.Id == request.OAuthConnectionId && x.IsDeleted == 0, cancellationToken);

        if (connection == null)
        {
            throw new BusinessException("未找到认证方式，请检查名称是否正确.");
        }

        if (connection.Name != request.Name)
        {
            var exist = await _databaseContext.OauthConnections
                .AnyAsync(x => x.Id != request.OAuthConnectionId && x.Name == request.Name && x.IsDeleted == 0, cancellationToken);

            if (exist)
            {
                throw new BusinessException("认证名称已存在，请更换后重试.");
            }
        }

        connection.Key = request.Key;
        connection.Name = request.Name;
        connection.IconUrl = request.IconUrl;
        connection.Provider = TextToJsonExtensions.ToJsonString(request.Provider);

        if (!string.IsNullOrEmpty(request.Secret))
        {
            connection.Secret = request.Secret;
        }

        if (request.Provider == OAuthPrivider.Custom)
        {
            connection.WellKnown = request.WellKnown.ToString();

            var oauthRedirectUrl = await GetAuthorizationEndpointAsync(request.WellKnown);
            connection.AuthorizeUrl = oauthRedirectUrl;
        }
        else if (request.Provider == OAuthPrivider.Feishu)
        {
            connection.AuthorizeUrl = "https://accounts.feishu.cn/open-apis/authen/v1/authorize";
            connection.WellKnown = "https://open.feishu.cn";
        }
        else if (request.Provider == OAuthPrivider.DingTalk)
        {
            connection.AuthorizeUrl = "https://login.dingtalk.com/oauth2/auth";
            connection.WellKnown = "https://login.dingtalk.com/oauth2/auth";
        }

        _databaseContext.Update(connection);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }

    private async Task<string> GetAuthorizationEndpointAsync(Uri wellKnownUrl)
    {
        if (wellKnownUrl == null)
        {
            throw new BusinessException("发现端点不能为空.");
        }

        var authClient = _authClientFactory.Create(new Uri(wellKnownUrl.GetLeftPart(UriPartial.Authority)));
        var wellKnown = await authClient.GetWellKnownAsync(wellKnownUrl.PathAndQuery.TrimStart('/'));

        return wellKnown.AuthorizationEndpoint;
    }
}
