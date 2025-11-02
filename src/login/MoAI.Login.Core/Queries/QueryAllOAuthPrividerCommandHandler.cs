using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Login.Models;
using MoAI.Login.Queries;
using MoAI.Login.Queries.Responses;
using System.Net;

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
                OAuthId = c.Id,
                Name = c.Name,
                IconUrl = c.IconUrl,
                Provider = c.Provider,
                RedirectUrl = c.AuthorizeUrl
            })
            .ToListAsync(cancellationToken);

        List<QueryAllOAuthPrividerCommandResponseItem> list = new();

        if (request.RedirectUrl != null && request.RedirectUrl.Host != new Uri(_systemOptions.WebUI).Host)
        {
            throw new BusinessException($"不合法的跳转地址") { StatusCode = 400 };
        }

        var loopbackRedirectUrl = _systemOptions.WebUI + $"/oauth_login";
        if (request.RedirectUrl != null)
        {
            loopbackRedirectUrl += $"?redirectUri={request.RedirectUrl}";
        }

        loopbackRedirectUrl = WebUtility.UrlEncode(loopbackRedirectUrl);

        foreach (var item in items)
        {
            var provider = item.Provider.JsonToObject<OAuthPrivider>();

            // 默认 OAuth 跳转地址格式
            var oauthRedirrctUrl = $"{item.RedirectUrl}?client_id={item.Key}&response_type=code&scope=openid%20profile&state={item.OAuthId}&redirect_uri={loopbackRedirectUrl}";

            if (provider == OAuthPrivider.Feishu)
            {
                // https://accounts.feishu.cn/open-apis/authen/v1/authorize?client_id=cli_a5d611352af9d00b&redirect_uri=https%3A%2F%2Fexample.com%2Fapi%2Foauth%2Fcallback&scope=bitable:app:readonly%20contact:contact&state=RANDOMSTRING
                oauthRedirrctUrl = $"{item.RedirectUrl}?client_id={item.Key}&response_type=code&scope=&state={item.OAuthId}&redirect_uri={loopbackRedirectUrl}";
            }
            else if (provider == OAuthPrivider.DingTalk)
            {
                // 钉钉的授权地址需要特殊处理
                // https://login.dingtalk.com/oauth2/auth?redirect_uri=https%3A%2F%2Fwww.aaaaa.com%2Fa%2Fb&response_type=code&client_id=dingbbbbbbb&scope=openid corpid&state=dddd&prompt=consent
                oauthRedirrctUrl = $"{item.RedirectUrl}?client_id={item.Key}&response_type=code&scope=openid%20corpid&state={item.OAuthId}&prompt=consent&redirect_uri={loopbackRedirectUrl}";
            }

            list.Add(new QueryAllOAuthPrividerCommandResponseItem
            {
                OAuthId = item.OAuthId,
                Name = item.Name,
                IconUrl = item.IconUrl,
                Provider = item.Provider,
                RedirectUrl = oauthRedirrctUrl
            });
        }

        return new QueryAllOAuthPrividerCommandResponse { Items = list };
    }
}