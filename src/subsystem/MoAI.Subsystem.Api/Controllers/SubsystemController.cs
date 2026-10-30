using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Account.Services;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Subsystem.Commands;
using MoAI.Subsystem.Queries;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Controllers;

/// <summary>
/// 子系统管理接口（仅超级管理员）.
/// </summary>
[ApiController]
[Route("/subsystem")]
public class SubsystemController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserAccountService _userAccountService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsystemController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userAccountService">用户账号服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public SubsystemController(IMediator mediator, IUserAccountService userAccountService, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userAccountService = userAccountService;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 查询子系统列表（仅 root），附带在线状态与授权团队数.
    /// </summary>
    /// <param name="search">按标识/名称模糊搜索，可选.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QuerySubsystemsCommandResponse"/>.</returns>
    [HttpGet("list")]
    public async Task<QuerySubsystemsCommandResponse> List([FromQuery] string? search, CancellationToken ct)
    {
        await EnsureRootAsync(ct);
        return await _mediator.Send(new QuerySubsystemsCommand { Search = search }, ct);
    }

    /// <summary>
    /// 创建子系统（仅 root），成功后返回接入密钥原文（仅此一次）.
    /// </summary>
    /// <param name="req">创建请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="CreateSubsystemCommandResponse"/>.</returns>
    [HttpPost("create")]
    public async Task<CreateSubsystemCommandResponse> Create([FromBody] CreateSubsystemCommand req, CancellationToken ct)
    {
        await EnsureRootAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 更新子系统（仅 root）：名称/描述/状态/团队授权范围.
    /// </summary>
    /// <param name="id">子系统 id.</param>
    /// <param name="req">更新请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}")]
    public async Task<EmptyCommandResponse> Update(Guid id, [FromBody] UpdateSubsystemCommand req, CancellationToken ct)
    {
        await EnsureRootAsync(ct);
        req.SubsystemId = id;
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 删除子系统（仅 root），软删除.
    /// </summary>
    /// <param name="id">子系统 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("{id}")]
    public async Task<EmptyCommandResponse> Delete(Guid id, CancellationToken ct)
    {
        await EnsureRootAsync(ct);
        return await _mediator.Send(new DeleteSubsystemCommand { SubsystemId = id }, ct);
    }

    /// <summary>
    /// 重置子系统接入密钥（仅 root），旧密钥立即失效.
    /// </summary>
    /// <param name="id">子系统 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="ResetSubsystemSecretCommandResponse"/>.</returns>
    [HttpPost("{id}/reset-secret")]
    public async Task<ResetSubsystemSecretCommandResponse> ResetSecret(Guid id, CancellationToken ct)
    {
        await EnsureRootAsync(ct);
        return await _mediator.Send(new ResetSubsystemSecretCommand { SubsystemId = id }, ct);
    }

    /// <summary>
    /// 查询子系统已授权团队 id 列表（仅 root），用于编辑回显.
    /// </summary>
    /// <param name="id">子系统 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QuerySubsystemAuthorizedTeamsCommandResponse"/>.</returns>
    [HttpGet("{id}/teams")]
    public async Task<QuerySubsystemAuthorizedTeamsCommandResponse> AuthorizedTeams(Guid id, CancellationToken ct)
    {
        await EnsureRootAsync(ct);
        return await _mediator.Send(new QuerySubsystemAuthorizedTeamsCommand { SubsystemId = id }, ct);
    }

    private async Task EnsureRootAsync(CancellationToken ct)
    {
        var userState = await _userAccountService.GetUserStateAsync(_userContextProvider.GetUserContext().UserId, ct);
        if (!userState.IsRoot)
        {
            throw new BusinessException("只有超级管理员可以管理子系统") { StatusCode = 403 };
        }
    }
}
