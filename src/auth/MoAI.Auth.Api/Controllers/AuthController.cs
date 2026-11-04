using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoAI.Auth.Commands;
using MoAI.Auth.Commands.Responses;
using MoAI.Auth.Queries;
using MoAI.Auth.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Auth.Controllers;

/// <summary>
/// 登录相关接口.
/// </summary>
[ApiController]
[Route("/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthController"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContextProvider"></param>
    public AuthController(IMediator mediator, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 用户登录：账号 + 密码 + 按策略携带的图形验证码与邮箱验证码一体验证，全部通过后颁发令牌.
    /// </summary>
    /// <param name="req">登录请求体（emailCode/captchaId/captchaCode 按策略必填，策略可经 challenge_policy 查询）.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="LoginCommandResponse"/>；secondFactor 为 totp 时表示需二次验证（凭 loginTicket 调 login/verify），为 none 时已直接颁发令牌.</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<LoginCommandResponse> Login([FromBody] LoginCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 登录二次验证：凭一次性登录票据提交认证器动态码（TOTP），通过后颁发令牌（邮箱验证码已并入 login 一体提交）.
    /// </summary>
    /// <param name="req">二次验证请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="LoginCommandResponse"/>.</returns>
    [HttpPost("login/verify")]
    [AllowAnonymous]
    public async Task<LoginCommandResponse> LoginVerify([FromBody] LoginVerifyCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 发送登录邮箱验证码（登录页一体化表单按用户名预发码；60 秒冷却 + IP 限流，图形验证码策略命中时需先过码）.
    /// </summary>
    /// <param name="req">发送请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>；账号不存在或未绑定邮箱时静默成功（防枚举）.</returns>
    [HttpPost("login_code/send")]
    [AllowAnonymous]
    public async Task<EmptyCommandResponse> SendLoginCode([FromBody] SendLoginCodeCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 获取新的图形验证码（20 次/分钟/IP）.
    /// </summary>
    /// <param name="req">查询请求（客户端信息自动回填）.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryCaptchaNewQueryResponse"/>.</returns>
    [HttpGet("captcha/new")]
    [AllowAnonymous]
    public Task<QueryCaptchaNewQueryResponse> NewCaptcha([FromQuery] QueryCaptchaNewQuery req, CancellationToken ct)
    {
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询当前客户端的验证策略（匿名）：登录/注册/找回密码页据此决定是否显示图形验证码与邮箱验证码输入框.
    /// </summary>
    /// <param name="req">查询请求（purpose：login / register / forgot；userName 可选）.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryChallengePolicyQueryResponse"/>.</returns>
    [HttpGet("challenge_policy")]
    [AllowAnonymous]
    public Task<QueryChallengePolicyQueryResponse> QueryChallengePolicy([FromQuery] QueryChallengePolicyQuery req, CancellationToken ct)
    {
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询当前用户两步验证（2FA）状态.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryTwoFactorStatusQueryResponse"/>.</returns>
    [HttpGet("two_factor/status")]
    public Task<QueryTwoFactorStatusQueryResponse> QueryTwoFactorStatus(CancellationToken ct)
    {
        var cmd = new QueryTwoFactorStatusQuery();
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 生成两步验证（2FA）绑定密钥：返回 base32 密钥与 otpauth URI（前端渲染二维码），需随后调 enable 验证动态码激活.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="SetupTwoFactorCommandResponse"/>.</returns>
    [HttpPost("two_factor/setup")]
    public async Task<SetupTwoFactorCommandResponse> SetupTwoFactor(CancellationToken ct)
    {
        var cmd = new SetupTwoFactorCommand();
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 启用两步验证（2FA）：校验认证器动态码后激活绑定.
    /// </summary>
    /// <param name="req">启用请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("two_factor/enable")]
    public async Task<EmptyCommandResponse> EnableTwoFactor([FromBody] EnableTwoFactorCommand req, CancellationToken ct)
    {
        _userContextProvider.SetUserContext(req);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 关闭两步验证（2FA）：校验当前动态码后解绑；系统强制开启时拒绝.
    /// </summary>
    /// <param name="req">关闭请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("two_factor/disable")]
    public async Task<EmptyCommandResponse> DisableTwoFactor([FromBody] DisableTwoFactorCommand req, CancellationToken ct)
    {
        _userContextProvider.SetUserContext(req);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 注册账号.
    /// </summary>
    /// <param name="req">注册请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="SimpleLong"/>（新建用户 ID）.</returns>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<SimpleLong> RegisterUser([FromBody] RegisterUserCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 发送邮箱验证码（注册 / 找回密码；注册验证码需系统开启，找回密码需 SMTP 邮件服务已启用；图形验证码策略命中时需先过码）.
    /// </summary>
    /// <param name="req">发送验证码请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("email_code/send")]
    [AllowAnonymous]
    public async Task<EmptyCommandResponse> SendEmailCode([FromBody] SendEmailCodeCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 通过邮箱验证码重置密码（找回密码；需 SMTP 邮件服务已启用）.
    /// </summary>
    /// <param name="req">重置密码请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("reset_password")]
    [AllowAnonymous]
    public async Task<EmptyCommandResponse> ResetPasswordByEmail([FromBody] ResetPasswordByEmailCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// OAuth 登录.
    /// </summary>
    /// <param name="req">OAuth 登录请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="OAuthLoginCommandResponse"/>.</returns>
    [HttpPost("oauth_login")]
    [AllowAnonymous]
    public async Task<OAuthLoginCommandResponse> OAuthLogin([FromBody] OAuthLoginCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// OAuth 注册.
    /// </summary>
    /// <param name="req">OAuth 注册请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="LoginCommandResponse"/>.</returns>
    [HttpPost("oauth_register")]
    [AllowAnonymous]
    public async Task<LoginCommandResponse> OAuthRegister([FromBody] OAuthRegisterCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 获取第三方登录列表.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAllOAuthPrividerCommandResponse"/>.</returns>
    [HttpGet("oauth_prividers")]
    [AllowAnonymous]
    public async Task<QueryAllOAuthPrividerCommandResponse> QueryAllOAuthProviders([FromQuery] Uri? redirectUrl, CancellationToken ct)
    {
        return await _mediator.Send(new QueryAllOAuthPrividerCommand { RedirectUrl = redirectUrl }, ct);
    }

    /// <summary>
    /// 刷新 token.
    /// </summary>
    /// <param name="req">刷新 token 请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="RefreshTokenCommandResponse"/>.</returns>
    [HttpPost("refresh_token")]
    [AllowAnonymous]
    public async Task<RefreshTokenCommandResponse> RefreshToken([FromBody] RefreshTokenCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }
}