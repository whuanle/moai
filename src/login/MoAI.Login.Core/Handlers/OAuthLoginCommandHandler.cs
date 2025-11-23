using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Infra;
using MoAI.Infra.Defaults;
using MoAI.Infra.DingTalk;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Feishu;
using MoAI.Infra.Feishu.Models;
using MoAI.Infra.OAuth;
using MoAI.Infra.OAuth.Models;
using MoAI.Login.Commands;
using MoAI.Login.Commands.Responses;
using MoAI.Login.Models;
using MoAI.Login.Services;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Login.Handlers;

/// <summary>
/// <inheritdoc cref="OAuthLoginCommand"/>
/// </summary>
public class OAuthLoginCommandHandler : IRequestHandler<OAuthLoginCommand, OAuthLoginCommandResponse>
{
    /*
     https://<HOST>/login/oauth/authorize?
client_id=CLIENT_ID&
redirect_uri=REDIRECT_URI&
response_type=code&
scope=openid&
state=STATE
     */

    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;
    private readonly IOAuthClient _authClient;
    private readonly IOAuthClientAccessToken _authClientAccessToken;
    private readonly IRedisDatabase _redisDatabase;
    private readonly ITokenProvider _tokenProvider;
    private readonly ILogger<OAuthLoginCommandHandler> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthLoginCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    /// <param name="authClient"></param>
    /// <param name="authClientAccessToken"></param>
    /// <param name="redisDatabase"></param>
    /// <param name="tokenProvider"></param>
    /// <param name="logger"></param>
    /// <param name="serviceProvider"></param>
    /// <param name="systemOptions"></param>
    public OAuthLoginCommandHandler(DatabaseContext databaseContext, IMediator mediator, IOAuthClient authClient, IOAuthClientAccessToken authClientAccessToken, IRedisDatabase redisDatabase, ITokenProvider tokenProvider, ILogger<OAuthLoginCommandHandler> logger, IServiceProvider serviceProvider, SystemOptions systemOptions)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
        _authClient = authClient;
        _authClientAccessToken = authClientAccessToken;
        _redisDatabase = redisDatabase;
        _tokenProvider = tokenProvider;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public async Task<OAuthLoginCommandResponse> Handle(OAuthLoginCommand request, CancellationToken cancellationToken)
    {
        var oauthConnectionEntity = await _databaseContext.OauthConnections
            .FirstOrDefaultAsync(c => c.Id == request.OAuthId, cancellationToken);

        if (oauthConnectionEntity == null)
        {
            throw new BusinessException("未找到认证方式") { StatusCode = 404 };
        }

        // 获取不同第三方登录下的用户标识
        OAuthBindUserProfile oauthUserProfile = default!;
        try
        {
            oauthUserProfile = await GetOpenIdUserInfo(request, oauthConnectionEntity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get openid error，request: {@Request},oauth privider name: {}", request, oauthConnectionEntity.Name);
            throw new BusinessException("第三方接口错误，请联系管理员") { StatusCode = 500 };
        }

        var userEntity = await _databaseContext.Users
            .Where(x => x.Id == _databaseContext.UserOauths.Where(a => a.ProviderId == oauthConnectionEntity.Id && a.Sub == oauthUserProfile.Profile.Sub).First().UserId).FirstOrDefaultAsync();

        // 没有绑定记录，则拒绝登录
        if (userEntity == null)
        {
            // 创建临时绑定记录
            var tempOauthBindId = Guid.CreateVersion7();
            var redisKey = $"oauth:bind:{tempOauthBindId}";
            await _redisDatabase.Database.StringSetAsync(redisKey, oauthUserProfile.ToRedisValue(), TimeSpan.FromMinutes(10));

            return new OAuthLoginCommandResponse
            {
                IsBindUser = false,
                TempOAuthBindId = tempOauthBindId,
                Name = oauthUserProfile.Name,
                OAuthId = oauthConnectionEntity.Id,
            };
        }

        // 已有绑定记录则直接登录.
        var userContext = new DefaultUserContext
        {
            UserId = userEntity.Id,
            UserName = userEntity.UserName,
            NickName = userEntity.NickName,
            Email = userEntity.Email
        };

        var (accessToken, refreshToken) = _tokenProvider.GenerateTokens(userContext);

        var result = new LoginCommandResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            UserId = userEntity.Id,
            UserName = userEntity.UserName,
            ExpiresIn = DateTimeOffset.Now.AddMinutes(30).ToUnixTimeMilliseconds()
        };

        _logger.LogInformation("User login.{@Message}", new { userEntity.Id, userEntity.UserName, userEntity.NickName });

        return new OAuthLoginCommandResponse
        {
            TempOAuthBindId = null,
            IsBindUser = true,
            LoginCommandResponse = result,
            OAuthId = oauthConnectionEntity.Id,
            Name = oauthUserProfile.Name
        };
    }

    private async Task<OAuthBindUserProfile> GetOpenIdUserInfo(OAuthLoginCommand request, Database.Entities.OauthConnectionEntity clientEntity)
    {
        var oauthPrivider = clientEntity.Provider.JsonToObject<OAuthPrivider>();

        if (oauthPrivider == OAuthPrivider.Feishu)
        {
            return await GetOpenIdFromFeishuAsync(request, clientEntity);
        }
        else if (oauthPrivider == OAuthPrivider.DingTalk)
        {
            return await GetOpenIdFromDingTalkAsync(request, clientEntity);
        }

        return await GetOpenIdFromCustomAsync(request, clientEntity);
    }

    private async Task<OAuthBindUserProfile> GetOpenIdFromCustomAsync(OAuthLoginCommand request, Database.Entities.OauthConnectionEntity clientEntity)
    {
        // 获取端点信息
        var wellKnownUrl = new Uri(clientEntity.WellKnown);
        _authClient.Client.BaseAddress = new Uri(wellKnownUrl.GetLeftPart(UriPartial.Authority));
        var wellKnown = await _authClient.GetWellKnownAsync(wellKnownUrl.PathAndQuery.TrimStart('/'));

        // 得到 accessToken 申请地址
        var accessTokenUrl = new Uri(wellKnown.TokenEndpoint);
        _authClientAccessToken.Client.BaseAddress = new Uri(accessTokenUrl.GetLeftPart(UriPartial.Authority));

        var openIdAccessToken = await _authClientAccessToken.GetAccessTokenAsync(accessTokenUrl.PathAndQuery.TrimStart('/'), new OpenIdAuthorizationRequest
        {
            ClientId = clientEntity.Key,
            ClientSecret = clientEntity.Secret,
            Code = request.Code,
            GrantType = "authorization_code"
        });

        // 得到用户信息地址
        var userInfoUrl = new Uri(wellKnown.UserinfoEndpoint);
        var userProfile = await _authClientAccessToken.GetUserInfoAsync(userInfoUrl.PathAndQuery.TrimStart('/'), openIdAccessToken.AccessToken);
        return new OAuthBindUserProfile
        {
            OAuthId = clientEntity.Id,
            Name = userProfile.Name,
            Profile = userProfile,
            AccessToken = openIdAccessToken.AccessToken
        };
    }

    private async Task<OAuthBindUserProfile> GetOpenIdFromDingTalkAsync(OAuthLoginCommand request, Database.Entities.OauthConnectionEntity clientEntity)
    {
        var dingTalkClient = _serviceProvider.GetRequiredService<IDingTalkClient>();
        var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();

        var dingTalkToken = await dingTalkClient.GetUserAccessTokenAsync(new UserAccessTokenRequest
        {
            ClientId = clientEntity.Key,
            ClientSecret = clientEntity.Secret,
            Code = request.Code,
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

    private async Task<OAuthBindUserProfile> GetOpenIdFromFeishuAsync(OAuthLoginCommand request, Database.Entities.OauthConnectionEntity clientEntity)
    {
        var feishuClient = _serviceProvider.GetRequiredService<IFeishuAuthClient>();
        var feishuAccessToken = await feishuClient.GetUserAccessTokenAsync(new FeishuTokenRequest
        {
            Code = request.Code,
            GrantType = "authorization_code",
            ClientId = clientEntity.Key,
            ClientSecret = clientEntity.Secret,
            RedirectUri = new Uri(new Uri(_systemOptions.WebUI), $"/oauth_login").ToString(),
            CodeVerifier = request.Code,
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