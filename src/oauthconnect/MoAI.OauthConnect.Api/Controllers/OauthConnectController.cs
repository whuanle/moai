using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Account.Services;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.OauthConnect.Commands;
using MoAI.OauthConnect.Queries;
using MoAI.OauthConnect.Queries.Responses;

namespace MoAI.OauthConnect.Controllers;

/// <summary>
/// 第三方登录连接管理接口.
/// </summary>
[ApiController]
[Route("/oauthconnect")]
public class OauthConnectController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserAccountService _userAccountService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="OauthConnectController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userAccountService">用户账号服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public OauthConnectController(IMediator mediator, IUserAccountService userAccountService, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userAccountService = userAccountService;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 查询全部第三方登录连接（仅管理员可访问）.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryAllOAuthConnectionCommandResponse"/>.</returns>
    [HttpGet("connections")]
    public async Task<QueryAllOAuthConnectionCommandResponse> QueryAll(CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new QueryAllOAuthConnectionCommand(), ct);
    }

    /// <summary>
    /// 创建第三方登录连接（仅管理员可访问）.
    /// </summary>
    /// <param name="req">创建请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("connections")]
    public async Task<EmptyCommandResponse> Create([FromBody] CreateOAuthConnectionCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 更新第三方登录连接（仅管理员可访问）.
    /// </summary>
    /// <param name="id">连接 id.</param>
    /// <param name="req">更新请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("connections/{id}")]
    public async Task<EmptyCommandResponse> Update(Guid id, [FromBody] UpdateOAuthConnectionCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        req.OAuthConnectionId = id;
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 删除第三方登录连接（仅管理员可访问）.
    /// </summary>
    /// <param name="id">连接 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("connections/{id}")]
    public async Task<EmptyCommandResponse> Delete(Guid id, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new DeleteOAuthConnectionCommand { OAuthConnectionId = id }, ct);
    }

    private async Task EnsureAdminAsync(CancellationToken ct)
    {
        var userState = await _userAccountService.GetUserStateAsync(_userContextProvider.GetUserContext().UserId, ct);
        if (!userState.IsAdmin)
        {
            throw new BusinessException("只有管理员可以管理第三方登录") { StatusCode = 403 };
        }
    }
}
