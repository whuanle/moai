using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Account.Services;
using MoAI.AIPlugin.Commands;
using MoAI.AIPlugin.Queries;
using MoAI.AIPlugin.Queries.Responses;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.AIPlugin.Controllers;

/// <summary>
/// 私有系统插件团队授权接口（仅管理员）.
/// </summary>
[ApiController]
[Route("/ai/plugin/{pluginId}/authorization")]
public class PluginTeamAuthorizationController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserAccountService _userAccountService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginTeamAuthorizationController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userAccountService">用户账号服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public PluginTeamAuthorizationController(IMediator mediator, IUserAccountService userAccountService, IUserContextProvider userContextProvider)
    {
        _mediator = mediator;
        _userAccountService = userAccountService;
        _userContextProvider = userContextProvider;
    }

    /// <summary>
    /// 查询系统插件的团队授权（仅管理员）：公开插件返回 isPublic=true 且 items 为空.
    /// </summary>
    /// <param name="pluginId">插件记录 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryPluginTeamAuthorizationCommandResponse"/>.</returns>
    [HttpGet]
    public async Task<QueryPluginTeamAuthorizationCommandResponse> Query(Guid pluginId, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        return await _mediator.Send(new QueryPluginTeamAuthorizationCommand { PluginId = pluginId }, ct);
    }

    /// <summary>
    /// 更新私有系统插件的团队授权，全量替换（仅管理员）；公开插件不允许设置.
    /// </summary>
    /// <param name="pluginId">插件记录 id.</param>
    /// <param name="req">请求体，携带授权团队 id 集合.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut]
    public async Task<EmptyCommandResponse> Update(Guid pluginId, [FromBody] UpdatePluginTeamAuthorizationCommand req, CancellationToken ct)
    {
        await EnsureAdminAsync(ct);
        req.PluginId = pluginId;
        return await _mediator.Send(req, ct);
    }

    private async Task EnsureAdminAsync(CancellationToken ct)
    {
        var userState = await _userAccountService.GetUserStateAsync(_userContextProvider.GetUserContext().UserId, ct);
        if (!userState.IsAdmin)
        {
            throw new BusinessException("只有管理员可以管理插件授权") { StatusCode = 403 };
        }
    }
}
