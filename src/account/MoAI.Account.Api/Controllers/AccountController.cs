using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoAI.Account.Commands;
using MoAI.Account.Queries;
using MoAI.Account.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Account.Controllers;

/// <summary>
/// 账号相关接口.
/// </summary>
[ApiController]
[Route("/account")]
public class AccountController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountController"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContextProvider"></param>
    public AccountController(IMediator mediator, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 使用 OAuth 绑定已存在的账号.
    /// </summary>
    /// <param name="req">绑定请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [Authorize]
    [HttpPost("oauth_bind_account")]
    public async Task<EmptyCommandResponse> OAuthBindExistAccount([FromBody] OAuthBindExistAccountCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 当前登录账号通过第三方授权回调 code 绑定第三方账号（与登录接口分离，需登录态）.
    /// </summary>
    /// <param name="req">绑定请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [Authorize]
    [HttpPost("oauth_bind")]
    public async Task<EmptyCommandResponse> OAuthBindAccountByCode([FromBody] OAuthBindAccountByCodeCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询用户基本信息.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="UserStateInfo"/>，包含用户状态信息.</returns>
    [HttpGet("userinfo")]
    public async Task<UserStateInfo> QueryUserInfo(CancellationToken ct)
    {
        var cmd = new QueryUserViewUserInfoCommand();
        _userContextProvider.SetUserContext(cmd);
        var result = await _mediator.Send(cmd, ct);

        // 待绑定两步验证 = token 携带 mfa_pending 且尚未绑定（绑定后旧 token 立即恢复，故按当前用户态修正）
        var claimPending = _userContextProvider.GetUserContext().Properties.TryGetValue("mfa_pending", out var mfaPending)
            && string.Equals(mfaPending, "true", StringComparison.OrdinalIgnoreCase);
        result.MfaPending = claimPending && !result.TwoFactorEnabled;
        return result;
    }

    /// <summary>
    /// 更新用户基本信息.
    /// </summary>
    /// <param name="req">更新用户信息请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [Authorize]
    [HttpPost("update_userinfo")]
    public async Task<EmptyCommandResponse> UpdateUserInfo([FromBody] UpdateUserInfoCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 重置用户密码.
    /// </summary>
    /// <param name="req">重置密码请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [Authorize]
    [HttpPost("reset_password")]
    public async Task<EmptyCommandResponse> ResetPassword([FromBody] ResetPasswordCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 更新用户头像.
    /// </summary>
    /// <param name="req">上传头像请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [Authorize]
    [HttpPost("avatar")]
    public async Task<EmptyCommandResponse> UpdateAvatar([FromBody] UpdateUserAvatarCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 解绑第三方账号.
    /// </summary>
    /// <param name="req">解绑请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [Authorize]
    [HttpPost("unbind_account")]
    public async Task<EmptyCommandResponse> UnbindAccount([FromBody] UnbindUserOAuthCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询当前用户已经绑定的第三方账号.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryUserBoundAccountsCommandResponse"/>.</returns>
    [Authorize]
    [HttpGet("bound_accounts")]
    public Task<QueryUserBoundAccountsCommandResponse> QueryBoundAccounts(CancellationToken ct)
    {
        var cmd = new QueryUserBoundAccountsCommand();
        _userContextProvider.SetUserContext(cmd);
        return _mediator.Send(cmd, ct);
    }
}
