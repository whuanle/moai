using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin.Authorization.Commands;
using MoAI.Plugin.Authorization.Queries;
using MoAI.Plugin.Authorization.Queries.Responses;

namespace MoAI.Plugin.Controllers;

/// <summary>
/// 插件授权管理.
/// </summary>
[ApiController]
[Route("/plugin/authorization")]
[EndpointGroupName("plugin")]
public class PluginAuthorizationController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginAuthorizationController"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public PluginAuthorizationController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 查询所有插件及其授权的团队列表.
    /// </summary>
    /// <param name="req">请求参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>插件授权列表.</returns>
    [HttpPost("plugins")]
    public async Task<QueryPluginAuthorizationsCommandResponse> QueryPluginAuthorizations(
        [FromBody] QueryPluginAuthorizationsCommand req,
        CancellationToken ct = default)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询所有团队及其授权的插件列表.
    /// </summary>
    /// <param name="req">请求参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>团队授权列表.</returns>
    [HttpPost("teams")]
    public async Task<QueryTeamAuthorizationsCommandResponse> QueryTeamAuthorizations(
        [FromBody] QueryTeamAuthorizationsCommand req,
        CancellationToken ct = default)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 修改某个插件的授权团队列表.
    /// </summary>
    /// <param name="req">请求参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpPost("plugin/update")]
    public async Task<EmptyCommandResponse> UpdatePluginAuthorizations(
        [FromBody] UpdatePluginAuthorizationsCommand req,
        CancellationToken ct = default)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 批量授权插件给某个团队.
    /// </summary>
    /// <param name="req">请求参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpPost("team/authorize")]
    public async Task<EmptyCommandResponse> BatchAuthorizePluginsToTeam(
        [FromBody] BatchAuthorizePluginsToTeamCommand req,
        CancellationToken ct = default)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 批量撤销某个团队的插件授权.
    /// </summary>
    /// <param name="req">请求参数.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpPost("team/revoke")]
    public async Task<EmptyCommandResponse> BatchRevokePluginsFromTeam(
        [FromBody] BatchRevokePluginsFromTeamCommand req,
        CancellationToken ct = default)
    {
        await CheckIsAdminAsync(ct);

        return await _mediator.Send(req, ct);
    }

    private async Task CheckIsAdminAsync(CancellationToken ct)
    {
        var isAdmin = await _mediator.Send(new QueryUserIsAdminCommand { ContextUserId = _userContext.UserId }, ct);
        if (!isAdmin.IsAdmin)
        {
            throw new BusinessException("没有操作权限.") { StatusCode = 403 };
        }
    }
}
