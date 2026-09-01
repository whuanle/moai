using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Infra.OAuth;
using MoAI.OauthConnect.Commands;

namespace MoAI.OauthConnect.Handlers;

/// <summary>
/// <inheritdoc cref="CreateOAuthConnectionCommand"/>
/// </summary>
public class CreateOAuthConnectionCommandHandler : IRequestHandler<CreateOAuthConnectionCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IOAuthClientFactory _authClientFactory;
    private readonly ILogger<CreateOAuthConnectionCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateOAuthConnectionCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="authClientFactory"></param>
    /// <param name="logger"></param>
    public CreateOAuthConnectionCommandHandler(DatabaseContext databaseContext, IOAuthClientFactory authClientFactory, ILogger<CreateOAuthConnectionCommandHandler> logger)
    {
        _databaseContext = databaseContext;
        _authClientFactory = authClientFactory;
        _logger = logger;
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

        if (request.Provider == OAuthPrivider.Feishu)
        {
            await AddFeishuConnectionAsync(request, cancellationToken);
        }
        else if (request.Provider == OAuthPrivider.DingTalk)
        {
            await AddDingTalkConnectionAsync(request, cancellationToken);
        }
        else
        {
            await AddCustomConnectionAsync(request, cancellationToken);
        }

        return EmptyCommandResponse.Default;
    }

    private async Task AddCustomConnectionAsync(CreateOAuthConnectionCommand request, CancellationToken cancellationToken)
    {
        var oauthRedirectUrl = await GetAuthorizationEndpointAsync(request.WellKnown);

        var connection = new OauthConnectionEntity
        {
            Name = request.Name,
            Provider = TextToJsonExtensions.ToJsonString(request.Provider),
            Key = request.Key,
            Secret = request.Secret,
            IconUrl = request.IconUrl,
            WellKnown = request.WellKnown.ToString(),
            AuthorizeUrl = oauthRedirectUrl,
        };

        _databaseContext.OauthConnections.Add(connection);
        await _databaseContext.SaveChangesAsync(cancellationToken);
    }

    private async Task AddFeishuConnectionAsync(CreateOAuthConnectionCommand request, CancellationToken cancellationToken)
    {
        var connection = new OauthConnectionEntity
        {
            Name = request.Name,
            Provider = TextToJsonExtensions.ToJsonString(request.Provider),
            Key = request.Key,
            Secret = request.Secret,
            IconUrl = request.IconUrl,
            AuthorizeUrl = "https://accounts.feishu.cn/open-apis/authen/v1/authorize",
            WellKnown = request.WellKnown?.ToString() ?? "https://open.feishu.cn",
        };

        _databaseContext.OauthConnections.Add(connection);
        await _databaseContext.SaveChangesAsync(cancellationToken);
    }

    private async Task AddDingTalkConnectionAsync(CreateOAuthConnectionCommand request, CancellationToken cancellationToken)
    {
        // see https://open.dingtalk.com/document/isvapp/tutorial-enabling-login-to-third-party-websites
        var connection = new OauthConnectionEntity
        {
            Name = request.Name,
            Provider = TextToJsonExtensions.ToJsonString(request.Provider),
            Key = request.Key,
            Secret = request.Secret,
            IconUrl = request.IconUrl,
            AuthorizeUrl = "https://login.dingtalk.com/oauth2/auth",
            WellKnown = request.WellKnown?.ToString() ?? "https://login.dingtalk.com/oauth2/auth",
        };

        _databaseContext.OauthConnections.Add(connection);
        await _databaseContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> GetAuthorizationEndpointAsync(Uri wellKnownUrl)
    {
        if (wellKnownUrl == null)
        {
            throw new BusinessException("发现端点不能为空.");
        }

        // 获取端点信息
        var authClient = _authClientFactory.Create(new Uri(wellKnownUrl.GetLeftPart(UriPartial.Authority)));
        var wellKnown = await authClient.GetWellKnownAsync(wellKnownUrl.PathAndQuery.TrimStart('/'));

        return wellKnown.AuthorizationEndpoint;
    }
}
