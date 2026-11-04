using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Auth.Commands;
using MoAI.Auth.Models;
using MoAI.Auth.Services;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.Auth.Handlers;

/// <summary>
/// <inheritdoc cref="SendLoginCodeCommand"/>.
/// 登录页一体化表单按用户名预发码：IP 限流检查 → 图形验证码校验（不消费，答对可继续用于登录提交）→
/// 用户存在且绑定邮箱才真发（否则静默成功防枚举）→ 60 秒冷却与发送由 EmailCodeService 内部保证 → 成功后计入 IP 发码限流.
/// </summary>
public class SendLoginCodeCommandHandler : IRequestHandler<SendLoginCodeCommand, EmptyCommandResponse>
{
    private const int SendCodeIpLimit = 10;

    private static readonly TimeSpan SendCodeIpWindow = TimeSpan.FromMinutes(10);

    private readonly ILoginSecurityService _loginSecurityService;
    private readonly ICaptchaService _captchaService;
    private readonly IPublicAuthRateLimiter _rateLimiter;
    private readonly IEmailCodeService _emailCodeService;
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="SendLoginCodeCommandHandler"/> class.
    /// </summary>
    public SendLoginCodeCommandHandler(
        ILoginSecurityService loginSecurityService,
        ICaptchaService captchaService,
        IPublicAuthRateLimiter rateLimiter,
        IEmailCodeService emailCodeService,
        DatabaseContext databaseContext)
    {
        _loginSecurityService = loginSecurityService;
        _captchaService = captchaService;
        _rateLimiter = rateLimiter;
        _emailCodeService = emailCodeService;
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(SendLoginCodeCommand request, CancellationToken cancellationToken)
    {
        var ip = request.ContextClientIp ?? string.Empty;

        // 只读检查 + 成功后才计数：冷却 429、验证码错误等失败尝试不占用 IP 发码额度
        await _rateLimiter.CheckAsync("send_code", ip, SendCodeIpLimit, cancellationToken);

        if (await _loginSecurityService.ShouldRequireCaptchaAsync(LoginCaptchaPurposes.Login, request.UserName, ip, cancellationToken))
        {
            var captchaValid = await _captchaService.VerifyAsync(
                request.CaptchaId ?? string.Empty,
                request.CaptchaCode ?? string.Empty,
                consumeOnSuccess: false,
                cancellationToken);
            if (!captchaValid)
            {
                throw new BusinessException("图形验证码错误或已过期，请刷新后重试.") { StatusCode = 400 };
            }
        }

        var email = await _databaseContext.Users
            .Where(u => (u.UserName == request.UserName || u.Email == request.UserName) && u.IsDeleted == 0 && !u.IsDisable)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrEmpty(email))
        {
            // 60 秒发送冷却由 EmailCodeService 内部保证（冷却 429 向客户端透出）
            await _emailCodeService.SendCodeAsync(EmailCodePurpose.Login, email, cancellationToken);
            await _rateLimiter.CountAsync("send_code", ip, SendCodeIpWindow, cancellationToken);
        }

        // 用户不存在或未绑定邮箱时静默成功（防枚举），验证码只发往真实账号邮箱
        return EmptyCommandResponse.Default;
    }
}
