using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MoAI.Common.Queries;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Commands;
using MoAI.Team.Queries;
using MoAI.Team.Queries.Responses;

namespace MoAI.Team.Controllers;

/// <summary>
/// 团队协作组操作接口.
/// </summary>
[ApiController]
[Route("/team/common")]
[EndpointGroupName("team")]
public class TeamCommonController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamCommonController"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public TeamCommonController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// 创建团队.
    /// </summary>
    /// <param name="req">创建团队命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>创建结果.</returns>
    [HttpPost("create")]
    public async Task<CreateTeamCommandResponse> Create([FromBody] CreateTeamCommand req, CancellationToken ct = default)
    {
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 按团队角度获取可用的 AI 模型列表（包含公开模型和团队被授权的模型）.
    /// </summary>
    /// <param name="req">请求体，见 <see cref="QueryTeamViewAiModelListCommand"/>.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryTeamViewAiModelListCommandResponse"/>，包含团队可用模型列表.</returns>
    [HttpPost("team_modellist")]
    public async Task<QueryTeamViewAiModelListCommandResponse> QueryTeamAiModelList([FromBody] QueryTeamViewAiModelListCommand req, CancellationToken ct = default)
    {
        await CheckIsMemberAsync(req.TeamId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 按团队角度获取可用的插件列表（包含公开插件、团队专属插件和团队被授权的插件）.
    /// </summary>
    /// <param name="req">请求体，见 <see cref="QueryTeamViewPluginListCommand"/>.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryTeamViewPluginListCommandResponse"/>，包含团队可用插件列表.</returns>
    [HttpPost("team_pluginlist")]
    public async Task<QueryTeamViewPluginListCommandResponse> QueryTeamPluginList([FromBody] QueryTeamViewPluginListCommand req, CancellationToken ct = default)
    {
        await CheckIsMemberAsync(req.TeamId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 退出团队.
    /// </summary>
    /// <param name="req">退出命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>操作结果.</returns>
    [HttpPost("leave")]
    public async Task<EmptyCommandResponse> Leave([FromBody] LeaveTeamCommand req, CancellationToken ct = default)
    {
        await CheckIsMemberAsync(req.TeamId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询用户在团队的角色.
    /// </summary>
    /// <param name="req">查询命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>用户角色.</returns>
    [HttpPost("role")]
    public async Task<QueryUserTeamRoleQueryResponse> GetUserRole([FromBody] QueryUserTeamRoleCommand req, CancellationToken ct = default)
    {
        await CheckIsMemberAsync(req.TeamId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 获取单个团队信息.
    /// </summary>
    /// <param name="req">查询命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>团队信息.</returns>
    [HttpPost("info")]
    public async Task<QueryTeamInfoCommandResponse> GetInfo([FromBody] QueryTeamInfoCommand req, CancellationToken ct = default)
    {
        await CheckIsMemberAsync(req.TeamId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询团队成员列表.
    /// </summary>
    /// <param name="req">查询命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>成员列表.</returns>
    [HttpPost("members")]
    public async Task<QueryTeamMemberListCommandResponse> GetMembers([FromBody] QueryTeamMemberListCommand req, CancellationToken ct = default)
    {
        await CheckIsMemberAsync(req.TeamId, ct);

        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询团队列表.
    /// </summary>
    /// <param name="req">查询命令.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>团队列表.</returns>
    [HttpPost("list")]
    public async Task<QueryTeamListCommandResponse> List([FromBody] QueryTeamListCommand req, CancellationToken ct = default)
    {
        return await _mediator.Send(req, ct);
    }

    private async Task CheckIsMemberAsync(int teamId, CancellationToken ct)
    {
        var existInTeam = await _mediator.Send(
            new QueryUserTeamRoleCommand
            {
                TeamId = teamId,
                ContextUserId = _userContext.UserId,
            },
            ct);

        if (existInTeam.Role < Models.TeamRole.Admin)
        {
            throw new BusinessException("不是该团队成员");
        }
    }
}
