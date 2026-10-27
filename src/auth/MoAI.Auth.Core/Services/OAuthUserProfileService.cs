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
using MoAI.Infra.WeixinWork;
using MoAI.Infra.WeixinWork.Models;

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
    public async Task<OAuthBindUserProfile> GetProfileAsync(Guid oAuthConnectionId, string code, Uri? redirectUrl, CancellationToken cancellationToken)
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
            return await GetOpenIdFromFeishuAsync(code, redirectUrl, oauthConnectionEntity);
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

    private async Task<OAuthBindUserProfile> GetOpenIdFromWeixinWorkAsync(string code, OauthConnectionEntity clientEntity)
    {
        var weixinWorkClient = _serviceProvider.GetRequiredService<IWeixinWorkClient>();

        var tokenResult = await weixinWorkClient.GetTokenAsync(clientEntity.Key, clientEntity.Secret);
        if (tokenResult.ErrCode != 0 || string.IsNullOrWhiteSpace(tokenResult.AccessToken))
        {
            throw new BusinessException("企业微信接口错误") { StatusCode = 500 };
        }

        var userInfoResult = await weixinWorkClient.GetUserInfoAsync(tokenResult.AccessToken, new GetUserInfoByCodeRequest
        {
            Code = code
        });

        if (userInfoResult.ErrCode != 0 || string.IsNullOrWhiteSpace(userInfoResult.UserId))
        {
            throw new BusinessException("企业微信接口错误") { StatusCode = 500 };
        }

        // 姓名与头像按通讯录接口补全，应用无通讯录读权限时失败不阻断登录，回退 userid
        var name = userInfoResult.UserId;
        var avatar = string.Empty;
        try
        {
            var userResult = await weixinWorkClient.GetUserAsync(tokenResult.AccessToken, userInfoResult.UserId);
            if (userResult.ErrCode == 0)
            {
                name = string.IsNullOrWhiteSpace(userResult.Name) ? name : userResult.Name;
                avatar = userResult.Avatar ?? string.Empty;
            }
        }
        catch (Exception)
        {
        }

        return new OAuthBindUserProfile
        {
            OAuthId = clientEntity.Id,
            Name = name,
            Profile = new OpenIdUserProfile
            {
                Sub = userInfoResult.UserId,
                Name = name,
                Audience = clientEntity.Key,
                Issuer = "https://work.weixin.qq.com",
                Picture = avatar,
                PreferredUsername = name,
            },

            AccessToken = tokenResult.AccessToken
        };
    }

    private async Task<OAuthBindUserProfile> GetOpenIdFromFeishuAsync(string code, Uri? redirectUrl, OauthConnectionEntity clientEntity)
    {
        // 飞书换 token 要求 redirect_uri 与授权时一致，这里用授权发起页的来源回填
        var loopbackOrigin = redirectUrl?.GetLeftPart(UriPartial.Authority) ?? _systemOptions.WebUI.TrimEnd('/');
        var feishuClient = _serviceProvider.GetRequiredService<IFeishuAuthClient>();
        var feishuAccessToken = await feishuClient.GetUserAccessTokenAsync(new FeishuTokenRequest
        {
            Code = code,
            GrantType = "authorization_code",
            ClientId = clientEntity.Key,
            ClientSecret = clientEntity.Secret,
            RedirectUri = $"{loopbackOrigin}/oauth_login",
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
