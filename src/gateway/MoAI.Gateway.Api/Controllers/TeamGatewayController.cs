using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Gateway.Queries;
using MoAI.Gateway.Queries.Responses;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Services;
using MoAI.Team.Services;

namespace MoAI.Gateway.Controllers;

/// <summary>
/// 团队模型网关接口：可用模型查询；接入统一使用应用接入 key（moai-ac-，应用接入页维护），本模块不再提供密钥管理.
/// </summary>
[ApiController]
[Route("/team/{teamId}/gateway")]
public class TeamGatewayController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextProvider _userContextProvider;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamGatewayController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    /// <param name="teamService">团队领域服务.</param>
    public TeamGatewayController(IMediator mediator, IUserContextProvider userContextProvider, ITeamService teamService)
    {
        _mediator = mediator;
        _userContextProvider = userContextProvider;
        _teamService = teamService;
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
        var role = await _teamService.GetMyRoleAsync(teamId, _userContextProvider.GetUserContext().UserId, ct);
        if (role == null)
        {
            throw new BusinessException("只有团队成员可以访问.") { StatusCode = 403 };
        }

        var cmd = new QueryTeamGatewayModelsCommand { TeamId = teamId };
        return await _mediator.Send(cmd, ct);
    }
}
