using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Database.Enums;
using MoAI.Gateway.Commands;
using MoAI.Gateway.Queries;
using MoAI.Gateway.Queries.Responses;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Team.Services;

namespace MoAI.Gateway.Controllers;

/// <summary>
/// 团队网关 API Key 管理接口.
/// </summary>
[ApiController]
[Route("/team/{teamId}/gateway")]
public class TeamApiKeyController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextProvider _userContextProvider;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamApiKeyController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    /// <param name="teamService">团队领域服务.</param>
    public TeamApiKeyController(IMediator mediator, IUserContextProvider userContextProvider, ITeamService teamService)
    {
        _mediator = mediator;
        _userContextProvider = userContextProvider;
        _teamService = teamService;
    }

    private long CurrentUserId => _userContextProvider.GetUserContext().UserId;

    /// <summary>
    /// 查询团队 API Key 列表，仅 Owner/Admin 可访问.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryTeamApiKeysCommandResponse"/>.</returns>
    [HttpGet("keys")]
    public async Task<QueryTeamApiKeysCommandResponse> QueryApiKeys([FromRoute] int teamId, CancellationToken ct)
    {
        await EnsureTeamManagerAsync(teamId, ct);
        var cmd = new QueryTeamApiKeysCommand { TeamId = teamId };
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 创建团队 API Key，密钥原文仅本次返回，仅 Owner/Admin 可操作.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="req">创建请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="CreateTeamApiKeyCommandResponse"/>.</returns>
    [HttpPost("keys")]
    public async Task<CreateTeamApiKeyCommandResponse> CreateApiKey([FromRoute] int teamId, [FromBody] CreateApiKeyRequest req, CancellationToken ct)
    {
        await EnsureTeamManagerAsync(teamId, ct);
        var cmd = new CreateTeamApiKeyCommand { TeamId = teamId, Name = req.Name, ExpireTime = req.ExpireTime };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 修改团队 API Key（名称、启用/禁用），仅 Owner/Admin 可操作.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="keyId">密钥 id.</param>
    /// <param name="req">修改请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("keys/{keyId}")]
    public async Task<EmptyCommandResponse> UpdateApiKey([FromRoute] int teamId, [FromRoute] Guid keyId, [FromBody] UpdateApiKeyRequest req, CancellationToken ct)
    {
        await EnsureTeamManagerAsync(teamId, ct);
        var cmd = new UpdateTeamApiKeyCommand { TeamId = teamId, ApiKeyId = keyId, Name = req.Name, IsDisable = req.IsDisable };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 删除团队 API Key，仅 Owner/Admin 可操作.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="keyId">密钥 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("keys/{keyId}")]
    public async Task<EmptyCommandResponse> DeleteApiKey([FromRoute] int teamId, [FromRoute] Guid keyId, CancellationToken ct)
    {
        await EnsureTeamManagerAsync(teamId, ct);
        var cmd = new DeleteTeamApiKeyCommand { TeamId = teamId, ApiKeyId = keyId };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 查询团队可用的网关模型与额度，团队成员可访问.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryTeamGatewayModelsCommandResponse"/>.</returns>
    [HttpGet("models")]
    public async Task<QueryTeamGatewayModelsCommandResponse> QueryModels([FromRoute] int teamId, CancellationToken ct)
    {
        await EnsureTeamMemberAsync(teamId, ct);
        var cmd = new QueryTeamGatewayModelsCommand { TeamId = teamId };
        return await _mediator.Send(cmd, ct);
    }

    private async Task EnsureTeamMemberAsync(int teamId, CancellationToken ct)
    {
        var role = await _teamService.GetMyRoleAsync(teamId, CurrentUserId, ct);
        if (role == null)
        {
            throw new BusinessException("只有团队成员可以访问.") { StatusCode = 403 };
        }
    }

    private async Task EnsureTeamManagerAsync(int teamId, CancellationToken ct)
    {
        var role = await _teamService.GetMyRoleAsync(teamId, CurrentUserId, ct);
        if (role is not (TeamRole.Admin or TeamRole.Owner))
        {
            throw new BusinessException("只有团队管理员可以管理 API Key.") { StatusCode = 403 };
        }
    }
}

/// <summary>
/// 创建 API Key 请求体.
/// </summary>
public class CreateApiKeyRequest
{
    /// <summary>
    /// 密钥名称.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 过期时间，null=永不过期.
    /// </summary>
    public DateTimeOffset? ExpireTime { get; set; }
}

/// <summary>
/// 修改 API Key 请求体.
/// </summary>
public class UpdateApiKeyRequest
{
    /// <summary>
    /// 新名称，null=不修改.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 是否禁用.
    /// </summary>
    public bool IsDisable { get; set; }
}
