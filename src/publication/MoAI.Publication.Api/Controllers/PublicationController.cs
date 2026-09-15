using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Account.Services;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Publication.Commands;
using MoAI.Publication.Queries;
using MoAI.Publication.Queries.Responses;

namespace MoAI.Publication.Controllers;

/// <summary>
/// 上架审核接口；团队申请/撤回上架，系统管理员审批，审批通过后目标资源 is_public 置为 true.
/// </summary>
[ApiController]
[Route("/publication")]
public class PublicationController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextProvider _userContextProvider;
    private readonly IUserAccountService _userAccountService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PublicationController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    /// <param name="userAccountService">用户账号服务，用于管理员门禁.</param>
    public PublicationController(IMediator mediator, IUserContextProvider userContextProvider, IUserAccountService userAccountService)
    {
        _mediator = mediator;
        _userContextProvider = userContextProvider;
        _userAccountService = userAccountService;
    }

    /// <summary>
    /// 申请上架，将团队资源（应用/提示词）提交到上架审核；需要资源所属团队 Admin 及以上角色.
    /// </summary>
    /// <param name="req">申请请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回上架审核记录 <see cref="SimpleLong"/>.</returns>
    [HttpPost("apply")]
    public Task<SimpleLong> Apply([FromBody] ApplyPublicationCommand req, CancellationToken ct)
    {
        _userContextProvider.SetUserContext(req);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 撤回上架申请，仅待审核状态可撤回；需要资源所属团队 Admin 及以上角色.
    /// </summary>
    /// <param name="req">撤回请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("withdraw")]
    public Task<EmptyCommandResponse> Withdraw([FromBody] WithdrawPublicationCommand req, CancellationToken ct)
    {
        _userContextProvider.SetUserContext(req);
        return _mediator.Send(req, ct);
    }

    /// <summary>
    /// 审批上架申请（通过/驳回），仅系统管理员可访问；通过后目标资源 is_public 置为 true.
    /// </summary>
    /// <param name="req">审批请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("review")]
    public async Task<EmptyCommandResponse> Review([FromBody] ReviewPublicationCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 分页查询全平台的上架审核列表（所有资源的上架申请在此可见），仅系统管理员可访问.
    /// </summary>
    /// <param name="req">查询参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryPublicationReviewListResponse"/>.</returns>
    [HttpGet("list")]
    public async Task<QueryPublicationReviewListResponse> QueryList([FromQuery] QueryPublicationReviewListCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询团队的上架审核列表（团队侧查看申请与审批状态），团队成员可访问.
    /// </summary>
    /// <param name="req">查询参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryPublicationReviewListResponse"/>.</returns>
    [HttpGet("team_list")]
    public Task<QueryPublicationReviewListResponse> QueryTeamList([FromQuery] QueryPublicationTeamListCommand req, CancellationToken ct)
    {
        _userContextProvider.SetUserContext(req);
        return _mediator.Send(req, ct);
    }

    private async Task EnsureAdminAsync(CancellationToken ct)
    {
        var userState = await _userAccountService.GetUserStateAsync(_userContextProvider.GetUserContext().UserId, ct);
        if (!userState.IsAdmin)
        {
            throw new BusinessException("只有系统管理员可以审批上架申请") { StatusCode = 403 };
        }
    }
}
