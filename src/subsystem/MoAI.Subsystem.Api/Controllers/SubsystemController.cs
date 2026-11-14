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
/// 系统级接入管理接口（仅超级管理员）：子系统与应用接入同一列表、同一套接口，
/// 仅 appType 与部分字段不同（子系统可配置委托 SSO 回调与团队/组织授权，应用接入即开即用）.
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
    /// 查询系统级接入列表（仅 root），子系统与应用接入同表回显，附带在线状态/在线实例数与授权概况.
    /// </summary>
    /// <param name="search">按标识/名称模糊搜索，可选.</param>
    /// <param name="appType">按接入类型过滤，可选.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QuerySubsysAppsCommandResponse"/>.</returns>
    [HttpGet("list")]
    public async Task<QuerySubsysAppsCommandResponse> List([FromQuery] string? search, [FromQuery] MoAI.Database.Enums.AccessAppType? appType, CancellationToken ct)
    {
        await EnsureRootAsync(ct);
        return await _mediator.Send(new QuerySubsysAppsCommand { Search = search, AppType = appType }, ct);
    }

    /// <summary>
    /// 创建系统级接入（仅 root），成功后返回 appid 与密钥原文（均仅此一次）.
    /// </summary>
    /// <param name="req">创建请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="CreateSubsysAppCommandResponse"/>.</returns>
    [HttpPost("create")]
    public async Task<CreateSubsysAppCommandResponse> Create([FromBody] CreateSubsysAppCommand req, CancellationToken ct)
    {
        await EnsureRootAsync(ct);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 更新系统级接入（仅 root）：名称/描述/状态；子系统行另支持团队授权范围与回调地址.
    /// </summary>
    /// <param name="id">接入 id.</param>
    /// <param name="req">更新请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}")]
    public async Task<EmptyCommandResponse> Update(Guid id, [FromBody] UpdateSubsysAppCommand req, CancellationToken ct)
    {
        await EnsureRootAsync(ct);
        req.Id = id;
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 删除系统级接入（仅 root，软删除），appid 立即失效.
    /// </summary>
    /// <param name="id">接入 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("{id}")]
    public async Task<EmptyCommandResponse> Delete(Guid id, CancellationToken ct)
    {
        await EnsureRootAsync(ct);
        return await _mediator.Send(new DeleteSubsysAppCommand { Id = id }, ct);
    }

    /// <summary>
    /// 重置系统级接入密钥（仅 root），旧密钥立即失效.
    /// </summary>
    /// <param name="id">接入 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="ResetSubsysAppSecretCommandResponse"/>.</returns>
    [HttpPost("{id}/reset-secret")]
    public async Task<ResetSubsysAppSecretCommandResponse> ResetSecret(Guid id, CancellationToken ct)
    {
        await EnsureRootAsync(ct);
        return await _mediator.Send(new ResetSubsysAppSecretCommand { Id = id }, ct);
    }

    /// <summary>
    /// 回显接入密钥原文（仅 root，RSA+AES 信封加密传输）：请求体携带 RSA(PKCS1) 加密的
    /// base64(AES-256 key)，响应以该 key 的 GCM 密文返回，端到端密文传输；存量哈希行 400 提示重置.
    /// </summary>
    /// <param name="id">接入 id.</param>
    /// <param name="req">回显请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="RevealSubsysAppSecretCommandResponse"/>.</returns>
    [HttpPost("{id}/reveal-secret")]
    public async Task<RevealSubsysAppSecretCommandResponse> RevealSecret(Guid id, [FromBody] RevealSubsysAppSecretCommand req, CancellationToken ct)
    {
        await EnsureRootAsync(ct);
        req.Id = id;
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询子系统已授权团队 id 列表（仅 root），用于编辑回显.
    /// </summary>
    /// <param name="id">接入 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QuerySubsystemAuthorizedTeamsCommandResponse"/>.</returns>
    [HttpGet("{id}/teams")]
    public async Task<QuerySubsystemAuthorizedTeamsCommandResponse> AuthorizedTeams(Guid id, CancellationToken ct)
    {
        await EnsureRootAsync(ct);
        return await _mediator.Send(new QuerySubsystemAuthorizedTeamsCommand { SubsystemId = id }, ct);
    }

    /// <summary>
    /// 查询子系统组织授权配置（仅 root），用于编辑回显；走统一组织授权表（org_grant，资源类型 subsystem）.
    /// </summary>
    /// <param name="id">接入 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QuerySubsystemOrgGrantsCommandResponse"/>.</returns>
    [HttpGet("{id}/org-grants")]
    public async Task<QuerySubsystemOrgGrantsCommandResponse> OrgGrants(Guid id, CancellationToken ct)
    {
        await EnsureRootAsync(ct);
        return await _mediator.Send(new QuerySubsystemOrgGrantsCommand { SubsystemId = id }, ct);
    }

    /// <summary>
    /// 整体覆盖子系统组织授权集合（仅 root），空列表=清空组织授权.
    /// </summary>
    /// <param name="id">接入 id.</param>
    /// <param name="req">授权请求体.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}/org-grants")]
    public async Task<EmptyCommandResponse> SetOrgGrants(Guid id, [FromBody] SetSubsystemOrgGrantsCommand req, CancellationToken ct)
    {
        await EnsureRootAsync(ct);
        req.SubsystemId = id;
        return await _mediator.Send(req, ct);
    }

    private async Task EnsureRootAsync(CancellationToken ct)
    {
        var userState = await _userAccountService.GetUserStateAsync(_userContextProvider.GetUserContext().UserId, ct);
        if (!userState.IsRoot)
        {
            throw new BusinessException("只有超级管理员可以管理系统级接入") { StatusCode = 403 };
        }
    }
}
