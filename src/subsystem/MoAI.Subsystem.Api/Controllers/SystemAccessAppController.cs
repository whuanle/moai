using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Account.Services;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Subsystem.Commands;
using MoAI.Subsystem.Commands.Responses;
using MoAI.Subsystem.Queries;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Controllers;

/// <summary>
/// 系统应用接入管理接口（仅超级管理员）：root 创建系统级接入 key，
/// 内部系统（如 ERP）凭 key 经 /api/subsys/system/* 维护系统资源（组织/用户等）.
/// </summary>
[ApiController]
[Route("/subsystem/system-app")]
public class SystemAccessAppController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserAccountService _userAccountService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemAccessAppController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userAccountService">用户账号服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public SystemAccessAppController(IMediator mediator, IUserAccountService userAccountService, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userAccountService = userAccountService;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 查询系统应用接入列表（仅 root）.
    /// </summary>
    /// <param name="search">按名称/key 前缀模糊搜索，可选.</param>
    /// <param name="pageNo">页码.</param>
    /// <param name="pageSize">每页数量.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QuerySystemAccessAppsCommandResponse"/>.</returns>
    [HttpGet("list")]
    public async Task<QuerySystemAccessAppsCommandResponse> List([FromQuery] string? search, [FromQuery] int pageNo = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        await EnsureRootAsync(ct);
        return await _mediator.Send(new QuerySystemAccessAppsCommand { Search = search, PageNo = pageNo, PageSize = pageSize }, ct);
    }

    /// <summary>
    /// 创建系统应用接入（仅 root），成功后返回 appid 与密钥原文（均仅此一次）.
    /// </summary>
    /// <param name="req">创建请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="CreateSystemAccessAppCommandResponse"/>.</returns>
    [HttpPost("create")]
    public async Task<CreateSystemAccessAppCommandResponse> Create([FromBody] CreateSystemAccessAppCommand req, CancellationToken ct)
    {
        await EnsureRootAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 更新系统应用接入（仅 root）：名称/描述/功能范围/状态.
    /// </summary>
    /// <param name="id">接入 id.</param>
    /// <param name="req">更新请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}")]
    public async Task<EmptyCommandResponse> Update(Guid id, [FromBody] UpdateSystemAccessAppCommand req, CancellationToken ct)
    {
        await EnsureRootAsync(ct);
        req.Id = id;
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 重置系统应用接入密钥（仅 root）：新密钥原文仅此一次返回，旧密钥立即失效.
    /// </summary>
    /// <param name="id">接入 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="ResetSystemAccessAppSecretCommandResponse"/>.</returns>
    [HttpPost("{id}/reset-secret")]
    public async Task<ResetSystemAccessAppSecretCommandResponse> ResetSecret(Guid id, CancellationToken ct)
    {
        await EnsureRootAsync(ct);
        return await _mediator.Send(new ResetSystemAccessAppSecretCommand { Id = id }, ct);
    }

    /// <summary>
    /// 删除系统应用接入（仅 root，软删除），key 立即失效.
    /// </summary>
    /// <param name="id">接入 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("{id}")]
    public async Task<EmptyCommandResponse> Delete(Guid id, CancellationToken ct)
    {
        await EnsureRootAsync(ct);
        return await _mediator.Send(new DeleteSystemAccessAppCommand { Id = id }, ct);
    }

    private async Task EnsureRootAsync(CancellationToken ct)
    {
        var userState = await _userAccountService.GetUserStateAsync(_userContextProvider.GetUserContext().UserId, ct);
        if (!userState.IsRoot)
        {
            throw new BusinessException("只有超级管理员可以管理系统应用接入") { StatusCode = 403 };
        }
    }
}
