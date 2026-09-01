using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Account.Commands;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Database.Helper;
using MoAI.Infra.Defaults;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Services;
using MoAI.Auth.Commands;
using MoAI.Auth.Commands.Responses;
using MoAI.Auth.Models;
using MoAI.Auth.Services;
using StackExchange.Redis.Extensions.Core.Abstractions;
using System.Transactions;

namespace MoAI.Auth.Handlers;

/// <summary>
/// <inheritdoc cref="OAuthRegisterCommand"/>
/// </summary>
public class OAuthRegisterCommandHandler : IRequestHandler<OAuthRegisterCommand, LoginCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;
    private readonly IRsaProvider _rsaProvider;
    private readonly IRedisDatabase _redisDatabase;
    private readonly ITokenProvider _tokenProvider;
    private readonly ILogger<OAuthRegisterCommandHandler> _logger;
    private readonly IUserAccountService _userAccountService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthRegisterCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    /// <param name="rsaProvider"></param>
    /// <param name="redisDatabase"></param>
    /// <param name="tokenProvider"></param>
    /// <param name="logger"></param>
    /// <param name="userAccountService"></param>
    public OAuthRegisterCommandHandler(DatabaseContext databaseContext, IMediator mediator, IRsaProvider rsaProvider, IRedisDatabase redisDatabase, ITokenProvider tokenProvider, ILogger<OAuthRegisterCommandHandler> logger, IUserAccountService userAccountService)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
        _rsaProvider = rsaProvider;
        _redisDatabase = redisDatabase;
        _tokenProvider = tokenProvider;
        _logger = logger;
        _userAccountService = userAccountService;
    }

    /// <inheritdoc/>
    public async Task<LoginCommandResponse> Handle(OAuthRegisterCommand request, CancellationToken cancellationToken)
    {
        // 绑定 OAuth 用户信息
        var redisKey = $"oauth:bind:{request.TempOAuthBindId}";
        var oauthBindUserProfile = await _redisDatabase.GetAsync<OAuthBindUserProfile>(redisKey);

        if (oauthBindUserProfile == null)
        {
            throw new BusinessException("第三方授权跳转登录已过期") { StatusCode = 403 };
        }

        var oauthConnectionEntity = await _databaseContext.OauthConnections.FirstOrDefaultAsync(c => c.Id == oauthBindUserProfile.OAuthId);

        if (oauthConnectionEntity == null)
        {
            throw new BusinessException("未找到对应的 OAuth 认证方式") { StatusCode = 404 };
        }

        var existingOpenIdUser = await _databaseContext.UserOauthConnections.Where(u => u.ProviderId == oauthConnectionEntity.Id && u.Sub == oauthBindUserProfile.Profile.Sub).AnyAsync();

        if (existingOpenIdUser)
        {
            throw new BusinessException("该 OAuth 用户已被注册") { StatusCode = 409 };
        }

        using TransactionScope transactionScope = TransactionScopeHelper.Create();

        var userName = "u" + Guid.CreateVersion7().ToString("N");

        // 占位手机号：phone 列非空且唯一，固定值会导致第二个 OAuth 用户注册失败，
        // 因此用 sub 的稳定哈希生成唯一占位号（9 前缀避开真实号段，满足注册正则）
        var subHashHex = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(oauthBindUserProfile.Profile.Sub ?? userName)));
        var subDigits = long.Parse(subHashHex[..13], System.Globalization.NumberStyles.HexNumber).ToString();
        var placeholderPhone = "9" + subDigits.PadRight(10, '0')[..10];

        var registerUserCommand = new RegisterUserCommand()
        {
            UserName = userName,
            Email = userName + "@moai.com",
            NickName = oauthBindUserProfile.Profile.PreferredUsername,
            Phone = placeholderPhone,
            Password = _rsaProvider.Encrypt(Guid.NewGuid().ToString("N"))
        };

        var userId = await _mediator.Send(registerUserCommand, cancellationToken);

        var user = await _databaseContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId.Value, cancellationToken);

        if (user == null)
        {
            throw new BusinessException("用户注册失败，请联系管理员") { StatusCode = 500 };
        }

        user.UserName = $"u{user.Id}";
        _databaseContext.Users.Update(user);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        await _userAccountService.RemoveUserStateAsync(user.Id, cancellationToken);

        await _databaseContext.UserOauthConnections.AddAsync(new Database.Entities.UserOauthConnectionEntity
        {
            UserId = userId.Value,
            ProviderId = oauthConnectionEntity.Id,
            Sub = oauthBindUserProfile.Profile.Sub
        });

        await _databaseContext.SaveChangesAsync();

        transactionScope.Complete();

        var userContext = new DefaultUserContext
        {
            UserId = user.Id,
            UserName = user.UserName,
            NickName = user.NickName,
            Email = user.Email
        };

        var (accessToken, refreshToken) = _tokenProvider.GenerateTokens(userContext);

        var result = new LoginCommandResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            UserId = user.Id,
            UserName = user.UserName,
            ExpiresIn = DateTimeOffset.Now.AddMinutes(30).ToUnixTimeMilliseconds()
        };

        _logger.LogInformation("User login.{@Message}", new { user.Id, user.UserName, user.NickName });

        return result;
    }
}
