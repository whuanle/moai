using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Auth.Commands;
using MoAI.Auth.Commands.Responses;
using MoAI.Auth.Models;
using MoAI.Auth.Services;
using MoAI.Database;
using MoAI.Infra.Defaults;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Service;
using MoAI.Infra.Services;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Auth.Handlers;

/// <summary>
/// <inheritdoc cref="LoginVerifyCommand"/>.
/// </summary>
public class LoginVerifyCommandHandler : IRequestHandler<LoginVerifyCommand, LoginCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ILoginChallengeService _challengeService;
    private readonly ILoginSecurityService _loginSecurityService;
    private readonly ITotpService _totpService;
    private readonly IEmailCodeService _emailCodeService;
    private readonly IAESProvider _aesProvider;
    private readonly ITokenProvider _tokenProvider;
    private readonly IRedisDatabase _redisDatabase;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginVerifyCommandHandler"/> class.
    /// </summary>
    public LoginVerifyCommandHandler(
        DatabaseContext databaseContext,
        ILoginChallengeService challengeService,
        ILoginSecurityService loginSecurityService,
        ITotpService totpService,
        IEmailCodeService emailCodeService,
        IAESProvider aesProvider,
        ITokenProvider tokenProvider,
        IRedisDatabase redisDatabase)
    {
        _databaseContext = databaseContext;
        _challengeService = challengeService;
        _loginSecurityService = loginSecurityService;
        _totpService = totpService;
        _emailCodeService = emailCodeService;
        _aesProvider = aesProvider;
        _tokenProvider = tokenProvider;
        _redisDatabase = redisDatabase;
    }

    /// <inheritdoc/>
    public async Task<LoginCommandResponse> Handle(LoginVerifyCommand request, CancellationToken cancellationToken)
    {
        // 票据一次性消费：防重放、防多端并发用同一票据
        var payload = await _challengeService.ConsumeTicketAsync(request.LoginTicket, cancellationToken);
        if (payload == null)
        {
            throw new BusinessException("登录会话已过期，请重新登录.") { StatusCode = 401 };
        }

        var user = await _databaseContext.Users
            .FirstOrDefaultAsync(u => u.Id == payload.UserId && u.IsDeleted == 0, cancellationToken);
        if (user == null || user.IsDisable)
        {
            throw new BusinessException("用户不存在或已被禁用") { StatusCode = 401 };
        }

        if (payload.Factor == LoginChallengeFactors.Totp)
        {
            await VerifyTotpAsync(user, request.Code, cancellationToken);
        }
        else if (payload.Factor == LoginChallengeFactors.EmailCode)
        {
            await _emailCodeService.VerifyAndConsumeAsync(EmailCodePurpose.Login, user.Email, request.Code, cancellationToken);
        }
        else
        {
            throw new BusinessException("登录会话已过期，请重新登录.") { StatusCode = 401 };
        }

        var mfaPending = await _loginSecurityService.IsForced2faAsync(cancellationToken) && !user.IsTotpEnabled;

        var userContext = new DefaultUserContext
        {
            UserId = user.Id,
            UserName = user.UserName,
            NickName = user.NickName,
            Email = user.Email,
            UserType = Infra.Models.UserType.Normal,
            // 强制 2FA 且未绑定：token 携带 mfa_pending，授权中间件只放行绑定流程相关接口
            Properties = mfaPending
                ? new Dictionary<string, string> { ["mfa_pending"] = "true" }
                : new Dictionary<string, string>()
        };

        var (accessToken, refreshToken) = _tokenProvider.GenerateTokens(userContext);

        return new LoginCommandResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            UserId = user.Id,
            UserName = user.UserName,
            ExpiresIn = DateTimeOffset.Now.AddMinutes(30).ToUnixTimeMilliseconds(),
            MfaPending = mfaPending
        };
    }

    /// <summary>
    /// 校验认证器动态码并防重放：同一时间步长的动态码 90 秒内只能使用一次.
    /// </summary>
    private async Task VerifyTotpAsync(Database.Entities.UserEntity user, string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(user.TotpSecret))
        {
            throw new BusinessException("登录会话已过期，请重新登录.") { StatusCode = 401 };
        }

        var secret = _aesProvider.Decrypt(user.TotpSecret);
        if (!_totpService.TryVerifyCode(secret, code, out var matchedTimeStep))
        {
            throw new BusinessException("动态码错误，请输入认证器当前显示的 6 位数字.") { StatusCode = 400 };
        }

        var usedKey = $"totp:used:{user.Id}";
        var usedStep = await _redisDatabase.GetAsync<long>(usedKey);
        if (usedStep >= matchedTimeStep)
        {
            throw new BusinessException("动态码已被使用，请等待认证器刷新后重试.") { StatusCode = 400 };
        }

        // 时间步长 30 秒，容差 ±1 步，TTL 90 秒覆盖整个可用窗口
        await _redisDatabase.Database.StringSetAsync(usedKey, matchedTimeStep, TimeSpan.FromSeconds(90));
    }
}
