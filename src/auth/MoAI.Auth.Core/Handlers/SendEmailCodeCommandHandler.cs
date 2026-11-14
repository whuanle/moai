using MediatR;
using MoAI.Auth.Commands;
using MoAI.Auth.Models;
using MoAI.Auth.Services;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.Auth.Handlers;

/// <summary>
/// <inheritdoc cref="SendEmailCodeCommand"/>.
/// 注册/找回密码发码：图形验证码策略命中时先过码（不消费）→ IP 发码限流 → 发送 → 计入 IP 发码限流与本目的发码计数
/// （发码计数驱动 after_fail 策略：同一 IP 首次发码免费，再发需要过图形验证码）.
/// </summary>
public class SendEmailCodeCommandHandler : IRequestHandler<SendEmailCodeCommand, EmptyCommandResponse>
{
    private const int SendCodeIpLimit = 10;

    private static readonly TimeSpan SendCodeIpWindow = TimeSpan.FromMinutes(10);

    private static readonly TimeSpan SendCountWindow = TimeSpan.FromHours(1);

    private readonly ILoginSecurityService _loginSecurityService;
    private readonly ICaptchaService _captchaService;
    private readonly IPublicAuthRateLimiter _rateLimiter;
    private readonly IEmailCodeService _emailCodeService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SendEmailCodeCommandHandler"/> class.
    /// </summary>
    /// <param name="loginSecurityService">登录安全策略服务.</param>
    /// <param name="captchaService">图形验证码服务.</param>
    /// <param name="rateLimiter">公共端点限流器.</param>
    /// <param name="emailCodeService">邮箱验证码服务.</param>
    public SendEmailCodeCommandHandler(
        ILoginSecurityService loginSecurityService,
        ICaptchaService captchaService,
        IPublicAuthRateLimiter rateLimiter,
        IEmailCodeService emailCodeService)
    {
        _loginSecurityService = loginSecurityService;
        _captchaService = captchaService;
        _rateLimiter = rateLimiter;
        _emailCodeService = emailCodeService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(SendEmailCodeCommand request, CancellationToken cancellationToken)
    {
        // 登录验证码由登录页一体化端点（/auth/login_code/send）发送，公共匿名端点拒绝，防止向任意邮箱轰炸验证码
        if (request.Purpose == EmailCodePurpose.Login)
        {
            throw new BusinessException("验证码用途无效.") { StatusCode = 400 };
        }

        var ip = request.ContextClientIp ?? string.Empty;
        var captchaPurpose = request.Purpose == EmailCodePurpose.Register ? LoginCaptchaPurposes.Register : LoginCaptchaPurposes.Forgot;

        // 只读检查 + 成功后才计数：冷却 429、验证码错误等失败尝试不占用 IP 发码额度
        await _rateLimiter.CheckAsync("send_code", ip, SendCodeIpLimit, cancellationToken);

        if (await _loginSecurityService.ShouldRequireCaptchaAsync(captchaPurpose, string.Empty, ip, cancellationToken))
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

        await _emailCodeService.SendCodeAsync(request.Purpose, request.Email, cancellationToken);

        await _rateLimiter.CountAsync("send_code", ip, SendCodeIpWindow, cancellationToken);
        await _rateLimiter.CountAsync($"sendct_{captchaPurpose}", ip, SendCountWindow, cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
