using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Auth.Models;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra;
using MoAI.Infra.DingTalk;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Feishu;
using MoAI.Infra.Feishu.Models;
using MoAI.Infra.OAuth;
using MoAI.Infra.OAuth.Models;

namespace MoAI.Auth.Services;

/// <summary>
/// <inheritdoc cref="IOAuthUserProfileService"/>
/// </summary>
public class OAuthUserProfileService : IOAuthUserProfileService
{
    private readonly DatabaseContext _databaseContext;
    private readonly IOAuthClientFactory _authClientFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthUserProfileService"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="authClientFactory"></param>
    /// <param name="serviceProvider"></param>
    /// <param name="systemOptions"></param>
    public OAuthUserProfileService(DatabaseContext databaseContext, IOAuthClientFactory authClientFactory, IServiceProvider serviceProvider, SystemOptions systemOptions)
    {
        _databaseContext = databaseContext;
        _authClientFactory = authClientFactory;
        _serviceProvider = serviceProvider;
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public async Task<OAuthBindUserProfile> GetProfileAsync(Guid oAuthConnectionId, string code, CancellationToken cancellationToken)
    {
        var oauthConnectionEntity = await _databaseContext.OauthConnections
            .FirstOrDefaultAsync(c => c.Id == oAuthConnectionId, cancellationToken);

        if (oauthConnectionEntity == null)
        {
            throw new BusinessException("未找到认证方式") { StatusCode = 404 };
        }

        var oauthPrivider = oauthConnectionEntity.Provider.JsonToObject<OAuthPrivider>();
        if (oauthPrivider == OAuthPrivider.Feishu)
        {
            return await GetOpenIdFromFeishuAsync(code, oauthConnectionEntity);
        }
        else if (oauthPrivider == OAuthPrivider.DingTalk)
        {
            return await GetOpenIdFromDingTalkAsync(code, oauthConnectionEntity);
        }

        return await GetOpenIdFromCustomAsync(code, oauthConnectionEntity);
    }

    private async Task<OAuthBindUserProfile> GetOpenIdFromCustomAsync(string code, OauthConnectionEntity clientEntity)
    {
        // 获取端点信息
        var wellKnownUrl = new Uri(clientEntity.WellKnown);
        var wellKnown = await _authClientFactory.Create(new Uri(wellKnownUrl.GetLeftPart(UriPartial.Authority)))
            .GetWellKnownAsync(wellKnownUrl.PathAndQuery.TrimStart('/'));

        // 得到 accessToken 申请地址
        var accessTokenUrl = new Uri(wellKnown.TokenEndpoint);
        var authClientAccessToken = _authClientFactory.CreateAccessTokenClient(new Uri(accessTokenUrl.GetLeftPart(UriPartial.Authority)));

        var openIdAccessToken = await authClientAccessToken.GetAccessTokenAsync(accessTokenUrl.PathAndQuery.TrimStart('/'), new OpenIdAuthorizationRequest
        {
            ClientId = clientEntity.Key,
            ClientSecret = clientEntity.Secret,
            Code = code,
            GrantType = "authorization_code"
        });

        // 得到用户信息地址
        var userInfoUrl = new Uri(wellKnown.UserinfoEndpoint);
        var userProfile = await authClientAccessToken.GetUserInfoAsync(userInfoUrl.PathAndQuery.TrimStart('/'), openIdAccessToken.AccessToken);
        return new OAuthBindUserProfile
        {
            OAuthId = clientEntity.Id,
            Name = userProfile.Name,
            Profile = userProfile,
            AccessToken = openIdAccessToken.AccessToken
        };
    }

    private async Task<OAuthBindUserProfile> GetOpenIdFromDingTalkAsync(string code, OauthConnectionEntity clientEntity)
    {
        var dingTalkClient = _serviceProvider.GetRequiredService<IDingTalkClient>();

        var dingTalkToken = await dingTalkClient.GetUserAccessTokenAsync(new UserAccessTokenRequest
        {
            ClientId = clientEntity.Key,
            ClientSecret = clientEntity.Secret,
            Code = code,
        });

        var dingTalkUserInfo = await dingTalkClient.GetContactUserInfoAsync("me", dingTalkToken.AccessToken!);

        return new OAuthBindUserProfile
        {
            OAuthId = clientEntity.Id,
            Name = dingTalkUserInfo.Nick!,
            Profile = new OpenIdUserProfile
            {
                Sub = dingTalkUserInfo.UnionId!,
                Name = dingTalkUserInfo.Nick!,
                Audience = clientEntity.Key,
                Issuer = "https://api.dingtalk.com",
                Picture = string.Empty,
                PreferredUsername = dingTalkUserInfo.Nick!,
            },

            AccessToken = string.Empty
        };
    }

    private async Task<OAuthBindUserProfile> GetOpenIdFromFeishuAsync(string code, OauthConnectionEntity clientEntity)
    {
        var feishuClient = _serviceProvider.GetRequiredService<IFeishuAuthClient>();
        var feishuAccessToken = await feishuClient.GetUserAccessTokenAsync(new FeishuTokenRequest
        {
            Code = code,
            GrantType = "authorization_code",
            ClientId = clientEntity.Key,
            ClientSecret = clientEntity.Secret,
            RedirectUri = new Uri(new Uri(_systemOptions.WebUI), $"/oauth_login").ToString(),
            CodeVerifier = code,
            Scope = string.Empty
        });

        if (feishuAccessToken.Code != 0)
        {
            throw new BusinessException("飞书接口错误");
        }

        var feishuUserInfo = await feishuClient.UserInfo("Bearer " + feishuAccessToken.AccessToken);
        if (feishuUserInfo.Code != 0)
        {
            throw new BusinessException("飞书接口错误");
        }

        return new OAuthBindUserProfile
        {
            OAuthId = clientEntity.Id,
            Name = feishuUserInfo.Data.Name,
            Profile = new OpenIdUserProfile
            {
                Sub = feishuUserInfo.Data.OpenId,
                Name = feishuUserInfo.Data.Name,
                Audience = clientEntity.Key,
                Issuer = "https://open.feishu.cn",
                Picture = feishuUserInfo.Data.AvatarUrl,
                PreferredUsername = feishuUserInfo.Data.Name,
            },

            AccessToken = feishuAccessToken.AccessToken
        };
    }
}
