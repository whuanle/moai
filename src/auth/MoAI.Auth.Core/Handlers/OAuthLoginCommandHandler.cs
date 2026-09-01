using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Defaults;
using MoAI.Infra.Exceptions;
using MoAI.Auth.Commands;
using MoAI.Auth.Commands.Responses;
using MoAI.Auth.Models;
using MoAI.Auth.Services;
using Microsoft.Extensions.Logging;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Auth.Handlers;

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
    private readonly IOAuthUserProfileService _profileService;
    private readonly IRedisDatabase _redisDatabase;
    private readonly ITokenProvider _tokenProvider;
    private readonly ILogger<OAuthLoginCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthLoginCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="profileService"></param>
    /// <param name="redisDatabase"></param>
    /// <param name="tokenProvider"></param>
    /// <param name="logger"></param>
    public OAuthLoginCommandHandler(DatabaseContext databaseContext, IOAuthUserProfileService profileService, IRedisDatabase redisDatabase, ITokenProvider tokenProvider, ILogger<OAuthLoginCommandHandler> logger)
    {
        _databaseContext = databaseContext;
        _profileService = profileService;
        _redisDatabase = redisDatabase;
        _tokenProvider = tokenProvider;
        _logger = logger;
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
            oauthUserProfile = await _profileService.GetProfileAsync(request.OAuthId, request.Code, cancellationToken);
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get openid error，request: {@Request},oauth privider name: {}", request, oauthConnectionEntity.Name);
            throw new BusinessException("第三方接口错误，请联系管理员") { StatusCode = 500 };
        }

        var userEntity = await _databaseContext.Users
            .Where(x => x.Id == _databaseContext.UserOauthConnections.Where(a => a.ProviderId == oauthConnectionEntity.Id && a.Sub == oauthUserProfile.Profile.Sub).First().UserId).FirstOrDefaultAsync(cancellationToken);

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
}
