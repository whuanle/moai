using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Infra;
using MoAI.Infra.Defaults;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Helpers;
using MoAI.Infra.Models;
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
/// 登录命令处理程序：一体化提交账号 + 密码 + 按策略携带的图形验证码与邮箱验证码，
/// 校验链为 图形验证码（消费）→ 密码 → 邮箱验证码（消费）→ TOTP 挑战（票据，由 login/verify 完成）→ 颁发令牌；
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
    private readonly ICaptchaService _captchaService;
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
    /// <param name="captchaService">图形验证码服务.</param>
    /// <param name="emailCodeService">邮箱验证码服务.</param>
    /// <param name="loginRiskService">登录风控服务.</param>
    /// <param name="logger"></param>
    public LoginCommandHandler(DatabaseContext dbContext, SystemOptions systemOptions, IRedisDatabase redisDatabase, IRsaProvider rsaProvider, ITokenProvider tokenProvider, ILoginChallengeService challengeService, ILoginSecurityService loginSecurityService, ICaptchaService captchaService, IEmailCodeService emailCodeService, ILoginRiskService loginRiskService, ILogger<LoginCommandHandler> logger)
    {
        _databaseContext = dbContext;
        _systemOptions = systemOptions;
        _redisDatabase = redisDatabase;
        _rsaProvider = rsaProvider;
        _tokenProvider = tokenProvider;
        _challengeService = challengeService;
        _loginSecurityService = loginSecurityService;
        _captchaService = captchaService;
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

        // 图形验证码是分级递进的第一道防线：密码校验前消费（一码一登录），错误不记入登录失败计数
        if (await _loginSecurityService.ShouldRequireCaptchaAsync(LoginCaptchaPurposes.Login, request.UserName, request.ContextClientIp ?? string.Empty, cancellationToken))
        {
            var captchaValid = await _captchaService.VerifyAsync(
                request.CaptchaId ?? string.Empty,
                request.CaptchaCode ?? string.Empty,
                consumeOnSuccess: true,
                cancellationToken);
            if (!captchaValid)
            {
                throw new BusinessException("图形验证码错误或已过期，请刷新后重试.") { StatusCode = 400 };
            }
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

        // 失败计数不在密码通过后立即清除：邮箱验证码未通过仍视为本次登录未完成，保留计数维持 after_fail 策略
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

        // 二次验证挑战：已绑定认证器（TOTP）优先，TOTP 用户跳过邮箱验证码（与历史行为一致）；
        // 票据接管后续验证，签发票据即完成本次密码验证，清除失败计数（与历史两段式行为一致）
        if (user.IsTotpEnabled)
        {
            await _redisDatabase.Database.KeyDeleteAsync(cacheKey);
            var totpTicket = await _challengeService.CreateTicketAsync(user.Id, LoginChallengeFactors.Totp, cancellationToken);
            return ChallengeResponse(user, LoginChallengeFactors.Totp, totpTicket);
        }

        // 登录邮箱验证码策略命中且账号绑定了邮箱：随登录请求一体校验（码由登录页预先发送），错码不记登录失败；
        // 验证未通过时不清除失败计数，维持 after_fail 策略（密码正确但请求不完整不代表登录完成）
        if (!string.IsNullOrEmpty(user.Email)
            && await _loginSecurityService.ShouldRequireLoginCodeAsync(failCount, cancellationToken))
        {
            if (string.IsNullOrEmpty(request.EmailCode))
            {
                throw new BusinessException("请输入邮箱验证码.") { StatusCode = 400 };
            }

            await _emailCodeService.VerifyAndConsumeAsync(EmailCodePurpose.Login, user.Email, request.EmailCode, cancellationToken);
        }

        // 全部验证通过，清除失败计数
        await _redisDatabase.Database.KeyDeleteAsync(cacheKey);

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
