using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Infra;
using MoAI.Infra.Defaults;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Helpers;
using MoAI.Infra.Services;
using MoAI.Auth.Commands;
using MoAI.Auth.Commands.Responses;
using MoAI.Auth.Models;
using MoAI.Auth.Services;
using MoAI.LoginSafety.Models;
using MoAI.LoginSafety.Services;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Auth.Handlers;

/// <summary>
/// 登录命令处理程序：密码校验通过后按需进入二次验证（认证器动态码 / 邮箱验证码），
/// 系统强制 2FA 且用户未绑定时签发带 mfa_pending 的受限 token；登录风控记录每次尝试并评估风险.
/// </summary>
public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly SystemOptions _systemOptions;
    private readonly IRedisDatabase _redisDatabase;
    private readonly IRsaProvider _rsaProvider;
    private readonly ITokenProvider _tokenProvider;
    private readonly ILoginChallengeService _challengeService;
    private readonly ILoginSecurityService _loginSecurityService;
    private readonly IEmailCodeService _emailCodeService;
    private readonly ILoginRiskService _loginRiskService;
    private readonly ILogger<LoginCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="systemOptions"></param>
    /// <param name="redisDatabase"></param>
    /// <param name="rsaProvider"></param>
    /// <param name="tokenProvider"></param>
    /// <param name="challengeService">登录二次验证票据服务.</param>
    /// <param name="loginSecurityService">登录安全策略服务.</param>
    /// <param name="emailCodeService">邮箱验证码服务.</param>
    /// <param name="loginRiskService">登录风控服务.</param>
    /// <param name="logger"></param>
    public LoginCommandHandler(DatabaseContext dbContext, SystemOptions systemOptions, IRedisDatabase redisDatabase, IRsaProvider rsaProvider, ITokenProvider tokenProvider, ILoginChallengeService challengeService, ILoginSecurityService loginSecurityService, IEmailCodeService emailCodeService, ILoginRiskService loginRiskService, ILogger<LoginCommandHandler> logger)
    {
        _databaseContext = dbContext;
        _systemOptions = systemOptions;
        _redisDatabase = redisDatabase;
        _rsaProvider = rsaProvider;
        _tokenProvider = tokenProvider;
        _challengeService = challengeService;
        _loginSecurityService = loginSecurityService;
        _emailCodeService = emailCodeService;
        _loginRiskService = loginRiskService;
        _logger = logger;
    }

    /// <summary>
    /// 处理登录命令.
    /// </summary>
    /// <param name="request">命令请求.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>登录结果.</returns>
    public async Task<LoginCommandResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // 登录风控：IP 封禁期内的请求直接拒绝（R1/R2 自动封禁或管理员手动封禁）
        await _loginRiskService.CheckIpBanAsync(request.ContextClientIp ?? string.Empty, cancellationToken);

        var cacheKey = $"login:fail:{request.UserName}";
        var failCount = await _redisDatabase.GetAsync<int>(cacheKey);

        if (failCount >= 5)
        {
            throw new BusinessException("登录失败次数过多，请稍后再试.") { StatusCode = 403 };
        }

        var user = await _databaseContext.Users.Where(u =>
                                  u.UserName == request.UserName || u.Email == request.UserName)
                              .FirstOrDefaultAsync(cancellationToken);

        if (user == null)
        {
            await RecordLoginFailAsync(request, "用户名或密码错误", cancellationToken);
            throw new BusinessException("用户名或密码错误") { StatusCode = 401 };
        }

        if (user.IsDisable)
        {
            await RecordLoginFailAsync(request, "用户已被禁用", cancellationToken, user.Id, user.Email);
            throw new BusinessException("用户已被禁用") { StatusCode = 401 };
        }

        try
        {
            var password = _rsaProvider.Decrypt(request.Password);
            if (!PBKDF2Helper.VerifyHash(password, user.Password, user.PasswordSalt))
            {
                await RecordLoginFailAsync(request, "用户名或密码错误", cancellationToken, user.Id, user.Email);
                throw new BusinessException("用户名或密码错误") { StatusCode = 401 };
            }
        }
        catch (Exception ex) when (ex is not BusinessException)
        {
            await RecordLoginFailAsync(request, "用户名或密码错误", cancellationToken, user.Id, user.Email);
            throw new BusinessException("用户名或密码错误") { StatusCode = 401 };
        }

        // 登录成功，清除失败计数
        await _redisDatabase.Database.KeyDeleteAsync(cacheKey);

        List<string> roles = new List<string>();

        var isRoot = await _databaseContext.Settings.AnyAsync(x => x.Key == "root" && x.Value == user.Id.ToString(), cancellationToken);

        if (isRoot)
        {
            roles.Add("root");
            roles.Add("admin");
        }
        else if (user.IsAdmin)
        {
            roles.Add("admin");
        }

        // 登录风控：密码校验成功即记录一次成功登录并评估风险（二次验证属于附加步骤，
        // 评估内部完成日志落库、风险事件、通知投递与 IP 封禁，任何内部异常不影响登录）
        _ = await _loginRiskService.EvaluateAsync(new LoginEventContext
        {
            UserId = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            IsAdmin = user.IsAdmin || isRoot,
            LoginType = LoginType.Password,
            IsSuccess = true,
            Ip = request.ContextClientIp ?? string.Empty,
            UserAgent = request.ContextUserAgent ?? string.Empty
        }, cancellationToken);

        // 二次验证挑战：已绑定认证器（TOTP）优先；未绑定则按登录邮箱验证码策略（策略命中且 SMTP 已启用）
        if (user.IsTotpEnabled)
        {
            var totpTicket = await _challengeService.CreateTicketAsync(user.Id, LoginChallengeFactors.Totp, cancellationToken);
            return ChallengeResponse(user, LoginChallengeFactors.Totp, totpTicket);
        }

        if (await _loginSecurityService.ShouldRequireLoginCodeAsync(failCount, cancellationToken))
        {
            try
            {
                await _emailCodeService.SendCodeAsync(EmailCodePurpose.Login, user.Email, cancellationToken);
            }
            catch (BusinessException ex) when (ex.StatusCode == 429)
            {
                // 60 秒发送冷却内重登：验证码仍在有效期，容忍 429 直接发票据让用户续用已有验证码
            }

            var emailTicket = await _challengeService.CreateTicketAsync(user.Id, LoginChallengeFactors.EmailCode, cancellationToken);
            return ChallengeResponse(user, LoginChallengeFactors.EmailCode, emailTicket);
        }

        // 强制 2FA 且未绑定：token 携带 mfa_pending，授权中间件只放行绑定流程相关接口
        var mfaPending = await _loginSecurityService.IsForced2faAsync(cancellationToken) && !user.IsTotpEnabled;

        var userContext = new DefaultUserContext
        {
            UserId = user.Id,
            UserName = user.UserName,
            NickName = user.NickName,
            Email = user.Email,
            UserType = Infra.Models.UserType.Normal,
            Properties = mfaPending
                ? new Dictionary<string, string> { ["mfa_pending"] = "true" }
                : new Dictionary<string, string>()
        };

        var (accessToken, refreshToken) = _tokenProvider.GenerateTokens(userContext);

        var result = new LoginCommandResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            UserId = user.Id,
            UserName = user.UserName,
            ExpiresIn = DateTimeOffset.Now.AddMinutes(30).ToUnixTimeMilliseconds(),
            MfaPending = mfaPending
        };

        _logger.LogInformation("User login.{@Message}", new { user.Id, user.UserName, user.NickName });

        // if (!string.IsNullOrEmpty(request.OAuthBindId))
        // {
        //    await BindOAuthAccount(request, user, result, cancellationToken);
        // }
        return result;
    }

    /// <summary>
    /// 构造二次验证挑战响应：不颁发令牌，返回一次性登录票据，由 login/verify 端点完成验证.
    /// </summary>
    /// <param name="user">用户实体.</param>
    /// <param name="factor">二次验证方式.</param>
    /// <param name="ticket">一次性登录票据.</param>
    /// <returns>登录挑战响应.</returns>
    private static LoginCommandResponse ChallengeResponse(Database.Entities.UserEntity user, string factor, string ticket)
    {
        return new LoginCommandResponse
        {
            UserId = user.Id,
            UserName = user.UserName,
            AccessToken = string.Empty,
            RefreshToken = string.Empty,
            ExpiresIn = 0,
            SecondFactor = factor,
            LoginTicket = ticket
        };
    }

    /// <summary>
    /// 记录登录失败：累加用户名失败计数并提交风控评估（R1/R2 IP 计数），随后由调用方抛出业务异常.
    /// </summary>
    /// <param name="request">登录命令.</param>
    /// <param name="failReason">失败原因.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <param name="userId">用户 id，用户不存在时为 0.</param>
    /// <param name="email">用户邮箱，可空.</param>
    /// <returns>异步任务.</returns>
    private async Task RecordLoginFailAsync(LoginCommand request, string failReason, CancellationToken cancellationToken, long userId = 0, string email = "")
    {
        await IncrementLoginFailCountAsync($"login:fail:{request.UserName}");

        await _loginRiskService.EvaluateAsync(new LoginEventContext
        {
            UserId = userId,
            UserName = request.UserName,
            Email = email,
            IsAdmin = false,
            LoginType = LoginType.Password,
            IsSuccess = false,
            FailReason = failReason,
            Ip = request.ContextClientIp ?? string.Empty,
            UserAgent = request.ContextUserAgent ?? string.Empty
        }, cancellationToken);
    }

    // private async Task BindOAuthAccount(LoginCommand request, UserEntity user, LoginCommandResponse result, CancellationToken cancellationToken)
    // {
    //    // 绑定 OAuth 用户信息
    //    var redisKey = $"oauth:bind:{request.OAuthBindId}";
    //    var oauthBIndUserProfile = await _redisDatabase.GetAsync<OAuthBindUserProfile>(redisKey);

    // if (oauthBIndUserProfile == null)
    //    {
    //        throw new BusinessException("检验第三方账号失败，请重新跳转登录.");
    //    }

    // var oauthUser = await _databaseContext.UserOauths
    //        .FirstOrDefaultAsync(o => o.Sub == oauthBIndUserProfile.Profile.Sub);

    // if (oauthUser != null)
    //    {
    //        if (oauthUser.UserId != user.Id)
    //        {
    //            await _redisDatabase.Database.KeyDeleteAsync(redisKey);
    //            throw new BusinessException("第三方账号已绑定其它账号.");
    //        }

    // // 重复绑定，不需要处理
    //        return;
    //    }

    // var oauthConnectionEntity = await _databaseContext.OauthConnections
    //        .FirstOrDefaultAsync(c => c.Id == oauthBIndUserProfile.OAuthId);

    // if (oauthConnectionEntity == null)
    //    {
    //        throw new BusinessException("未找到对应的 OAuth 认证方式") { StatusCode = 404 };
    //    }

    // var oauthEntity = new UserOauthEntity
    //    {
    //        UserId = user.Id,
    //        ProviderId = oauthConnectionEntity.Id,
    //        Sub = oauthBIndUserProfile.Profile.Sub,
    //    };

    // await _databaseContext.UserOauths.AddAsync(oauthEntity, cancellationToken);
    //    await _databaseContext.SaveChangesAsync(cancellationToken);

    // await _redisDatabase.Database.KeyDeleteAsync(redisKey);
    // }

    /// <summary>
    /// 增加登录失败计数.
    /// </summary>
    /// <param name="cacheKey">缓存键.</param>
    /// <returns>异步任务.</returns>
    private async Task IncrementLoginFailCountAsync(string cacheKey)
    {
        var failCount = await _redisDatabase.Database.StringIncrementAsync(cacheKey);
        await _redisDatabase.Database.KeyExpireAsync(cacheKey, TimeSpan.FromMinutes(5));
    }
}