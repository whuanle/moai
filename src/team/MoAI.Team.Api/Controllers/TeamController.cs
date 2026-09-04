using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Team.Commands;
using MoAI.Team.Queries;
using MoAI.Team.Queries.Responses;
using MoAI.Team.Services;

namespace MoAI.Team.Controllers;

/// <summary>
/// 团队接口.
/// </summary>
[ApiController]
[Route("/team")]
public class TeamController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextProvider _userContextProvider;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例，用于发送命令/查询.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    /// <param name="teamService">团队领域服务.</param>
    public TeamController(IMediator mediator, IUserContextProvider userContextProvider, ITeamService teamService)
    {
        _mediator = mediator;
        _userContextProvider = userContextProvider;
        _teamService = teamService;
    }

    private long CurrentUserId => _userContextProvider.GetUserContext().UserId;

    /// <summary>
    /// 创建团队，创建者自动成为团队所有者.
    /// </summary>
    /// <param name="req">创建请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回团队 <see cref="SimpleLong"/>.</returns>
    [HttpPost]
    public async Task<SimpleLong> CreateTeam([FromBody] CreateTeamCommand req, CancellationToken ct)
    {
        _userContextProvider.SetUserContext(req);
        return await _mediator.Send(req, ct);
    }

    /// <summary>
    /// 查询我参与的团队列表.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryTeamsCommandResponse"/>.</returns>
    [HttpGet("list")]
    public async Task<QueryTeamsCommandResponse> QueryTeams(CancellationToken ct)
    {
        var cmd = new QueryTeamsCommand();
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 查询团队详情，仅团队成员可访问.
    /// </summary>
    /// <param name="id">团队 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryTeamCommandResponse"/>.</returns>
    [HttpGet("{id}")]
    public async Task<QueryTeamCommandResponse> QueryTeam(long id, CancellationToken ct)
    {
        await EnsureTeamMemberAsync(id, ct);
        var cmd = new QueryTeamCommand { TeamId = id };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 修改团队信息，仅 Owner/Admin 可操作.
    /// </summary>
    /// <param name="id">团队 id.</param>
    /// <param name="req">修改请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}")]
    public async Task<EmptyCommandResponse> UpdateTeam(long id, [FromBody] UpdateTeamCommand req, CancellationToken ct)
    {
        await EnsureTeamManagerAsync(id, "只有团队管理员可以修改团队信息.", ct);
        var cmd = new UpdateTeamCommand { TeamId = id, Name = req.Name, Description = req.Description };
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 解散团队，仅 Owner 可操作.
    /// </summary>
    /// <param name="id">团队 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("{id}")]
    public async Task<EmptyCommandResponse> DissolveTeam(long id, CancellationToken ct)
    {
        await EnsureOwnerAsync(id, "只有团队所有者可以解散团队.", ct);
        return await _mediator.Send(new DissolveTeamCommand { TeamId = id }, ct);
    }

    /// <summary>
    /// 转让团队所有权，仅 Owner 可操作，原 Owner 降为 Admin.
    /// </summary>
    /// <param name="id">团队 id.</param>
    /// <param name="req">转让请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}/owner")]
    public async Task<EmptyCommandResponse> UpdateTeamOwner(long id, [FromBody] UpdateTeamOwnerCommand req, CancellationToken ct)
    {
        await EnsureOwnerAsync(id, "只有团队所有者可以转让所有权.", ct);
        var cmd = new UpdateTeamOwnerCommand { TeamId = id, UserId = req.UserId };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 设置团队头像，仅 Owner/Admin 可操作.
    /// </summary>
    /// <param name="id">团队 id.</param>
    /// <param name="req">头像请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("{id}/avatar")]
    public async Task<EmptyCommandResponse> UpdateTeamAvatar(long id, [FromBody] UpdateTeamAvatarCommand req, CancellationToken ct)
    {
        await EnsureTeamManagerAsync(id, "只有团队管理员可以设置团队头像.", ct);
        var cmd = new UpdateTeamAvatarCommand { TeamId = id, ObjectKey = req.ObjectKey };
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 查询团队成员列表，仅团队成员可访问.
    /// </summary>
    /// <param name="id">团队 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryTeamUsersCommandResponse"/>.</returns>
    [HttpGet("{id}/users")]
    public async Task<QueryTeamUsersCommandResponse> QueryTeamUsers(long id, CancellationToken ct)
    {
        await EnsureTeamMemberAsync(id, ct);
        return await _mediator.Send(new QueryTeamUsersCommand { TeamId = id }, ct);
    }

    /// <summary>
    /// 查询可邀请的候选用户（按用户名/昵称/邮箱模糊），仅 Owner/Admin 可访问，已入团成员会被排除.
    /// </summary>
    /// <param name="id">团队 id.</param>
    /// <param name="keyword">搜索关键字.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="QueryTeamCandidatesCommandResponse"/>.</returns>
    [HttpGet("{id}/candidates")]
    public async Task<QueryTeamCandidatesCommandResponse> QueryTeamCandidates(long id, [FromQuery] string? keyword, CancellationToken ct)
    {
        await EnsureTeamManagerAsync(id, "只有团队管理员可以邀请成员.", ct);
        return await _mediator.Send(new QueryTeamCandidatesCommand { TeamId = id, Keyword = keyword }, ct);
    }

    /// <summary>
    /// 添加团队成员，仅 Owner/Admin 可操作；授予 Admin 需要 Owner.
    /// </summary>
    /// <param name="id">团队 id.</param>
    /// <param name="req">添加请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPost("{id}/users")]
    public async Task<EmptyCommandResponse> AddTeamUser(long id, [FromBody] AddTeamUserCommand req, CancellationToken ct)
    {
        await EnsureTeamManagerAsync(id, "只有团队管理员可以添加成员.", ct);
        if (req.Role == TeamRole.Admin)
        {
            await EnsureOwnerAsync(id, "只有团队所有者可以授予管理员角色.", ct);
        }

        var cmd = new AddTeamUserCommand { TeamId = id, UserId = req.UserId, Role = req.Role };
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 修改成员角色，仅 Owner 可操作.
    /// </summary>
    /// <param name="id">团队 id.</param>
    /// <param name="userId">目标用户 id.</param>
    /// <param name="req">角色请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpPut("{id}/user/{userId}/role")]
    public async Task<EmptyCommandResponse> UpdateTeamUserRole(long id, long userId, [FromBody] UpdateTeamUserRoleCommand req, CancellationToken ct)
    {
        await EnsureOwnerAsync(id, "只有团队所有者可以调整成员角色.", ct);
        var cmd = new UpdateTeamUserRoleCommand { TeamId = id, UserId = userId, Role = req.Role };
        _userContextProvider.SetUserContext(cmd);
        return await _mediator.Send(cmd, ct);
    }

    /// <summary>
    /// 移除团队成员；成员也可用此接口自行退出.
    /// </summary>
    /// <param name="id">团队 id.</param>
    /// <param name="userId">目标用户 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>返回 <see cref="EmptyCommandResponse"/>.</returns>
    [HttpDelete("{id}/user/{userId}")]
    public async Task<EmptyCommandResponse> RemoveTeamUser(long id, long userId, CancellationToken ct)
    {
        await EnsureCanRemoveAsync(id, userId, ct);
        return await _mediator.Send(new RemoveTeamUserCommand { TeamId = id, UserId = userId }, ct);
    }

    private async Task EnsureTeamMemberAsync(long teamId, CancellationToken ct)
    {
        var myRole = await _teamService.GetMyRoleAsync(teamId, CurrentUserId, ct);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }
    }

    private async Task EnsureTeamManagerAsync(long teamId, string denyMessage, CancellationToken ct)
    {
        var myRole = await _teamService.GetMyRoleAsync(teamId, CurrentUserId, ct);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException(denyMessage) { StatusCode = 403 };
        }
    }

    private async Task EnsureOwnerAsync(long teamId, string denyMessage, CancellationToken ct)
    {
        var myRole = await _teamService.GetMyRoleAsync(teamId, CurrentUserId, ct);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole != TeamRole.Owner)
        {
            throw new BusinessException(denyMessage) { StatusCode = 403 };
        }
    }

    private async Task EnsureCanRemoveAsync(long teamId, long userId, CancellationToken ct)
    {
        var myRole = await _teamService.GetMyRoleAsync(teamId, CurrentUserId, ct);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (userId == CurrentUserId)
        {
            if (myRole == TeamRole.Owner)
            {
                throw new BusinessException("团队所有者不能退出团队，请先解散团队.") { StatusCode = 400 };
            }

            return;
        }

        var targetRole = await _teamService.GetMyRoleAsync(teamId, userId, ct);
        if (targetRole == null)
        {
            throw new BusinessException("目标用户不是团队成员.") { StatusCode = 404 };
        }

        if (targetRole == TeamRole.Owner)
        {
            throw new BusinessException("不能移除团队所有者.") { StatusCode = 400 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以移除成员.") { StatusCode = 403 };
        }

        if (myRole == TeamRole.Admin && targetRole == TeamRole.Admin)
        {
            throw new BusinessException("管理员不能移除其他管理员.") { StatusCode = 403 };
        }
    }
}
