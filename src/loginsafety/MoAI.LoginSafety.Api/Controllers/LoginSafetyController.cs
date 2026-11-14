using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Account.Services;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.LoginSafety.Commands;

namespace MoAI.LoginSafety.Controllers;

/// <summary>
/// 登录安全风控接口（仅管理员）：登录日志、风险事件、IP 封禁管理.
/// </summary>
[ApiController]
[Route("/loginsafety")]
public class LoginSafetyController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserAccountService _userAccountService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginSafetyController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例.</param>
    /// <param name="userAccountService">用户账号服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public LoginSafetyController(IMediator mediator, IUserAccountService userAccountService, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userAccountService = userAccountService;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 分页查询登录日志.
    /// </summary>
    /// <param name="req">查询请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="Queries.Responses.QueryLoginLogCommandResponse"/>.</returns>
    [HttpGet("loginlog")]
    public async Task<Queries.Responses.QueryLoginLogCommandResponse> QueryLoginLog([FromQuery] QueryLoginLogCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 分页查询登录风险事件.
    /// </summary>
    /// <param name="req">查询请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="Queries.Responses.QueryRiskEventListCommandResponse"/>.</returns>
    [HttpGet("riskevent")]
    public async Task<Queries.Responses.QueryRiskEventListCommandResponse> QueryRiskEvent([FromQuery] QueryRiskEventListCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 批量标记风险事件为已读.
    /// </summary>
    /// <param name="req">标记请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回空响应.</returns>
    [HttpPost("riskevent/read")]
    public async Task<EmptyCommandResponse> MarkRiskEventRead([FromBody] MarkRiskEventReadCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询未读风险事件数量（菜单「日志事件」角标数据源）.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回未读数量.</returns>
    [HttpGet("riskevent/unread-count")]
    public async Task<QueryRiskEventUnreadCountCommandResponse> QueryUnreadCount(CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new QueryRiskEventUnreadCountCommand(), ct);
    }

    /// <summary>
    /// 分页查询 IP 封禁记录.
    /// </summary>
    /// <param name="req">查询请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="Queries.Responses.QueryIpBanListCommandResponse"/>.</returns>
    [HttpGet("ipban")]
    public async Task<Queries.Responses.QueryIpBanListCommandResponse> QueryIpBan([FromQuery] QueryIpBanListCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 解封 IP.
    /// </summary>
    /// <param name="req">解封请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回空响应.</returns>
    [HttpPost("ipban/lift")]
    public async Task<EmptyCommandResponse> LiftIpBan([FromBody] LiftIpBanCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 手动封禁 IP.
    /// </summary>
    /// <param name="req">封禁请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回空响应.</returns>
    [HttpPost("ipban/add")]
    public async Task<EmptyCommandResponse> AddIpBan([FromBody] AddIpBanCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询当前用户本人的登录记录（仅成功登录）.
    /// </summary>
    /// <param name="req">查询请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="Queries.Responses.QueryMyLoginLogCommandResponse"/>.</returns>
    [HttpGet("mylog")]
    public async Task<Queries.Responses.QueryMyLoginLogCommandResponse> QueryMyLog([FromQuery] QueryMyLoginLogCommand req, CancellationToken ct)
    {
        return await _mediator.Send(req, ct);
    }

    private async Task EnsureAdminAsync(CancellationToken ct)
    {
        var userState = await _userAccountService.GetUserStateAsync(_userContextProvider.GetUserContext().UserId, ct);
        if (!userState.IsAdmin)
        {
            throw new BusinessException("只有管理员可以查看登录安全风控数据") { StatusCode = 403 };
        }
    }
}
