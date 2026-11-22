using Mediator;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Events;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Commands;
using MoAI.Team.Services;

namespace MoAI.Team.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateTeamUserRoleCommand"/>
/// </summary>
public class UpdateTeamUserRoleCommandHandler : IRequestHandler<UpdateTeamUserRoleCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IAccessEventPublisher _eventPublisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTeamUserRoleCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="eventPublisher">接入事件发布器.</param>
    public UpdateTeamUserRoleCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IAccessEventPublisher eventPublisher)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _eventPublisher = eventPublisher;
    }

    /// <inheritdoc/>
    public async ValueTask<EmptyCommandResponse> Handle(UpdateTeamUserRoleCommand request, CancellationToken cancellationToken)
    {
        if (request.UserId == request.ContextUserId)
        {
            throw new BusinessException("不能修改自己的角色，所有权转让请后续版本支持.") { StatusCode = 400 };
        }

        var member = await _databaseContext.TeamUsers
            .FirstOrDefaultAsync(x => x.TeamId == request.TeamId && x.UserId == request.UserId, cancellationToken);

        if (member == null)
        {
            throw new BusinessException("目标用户不是团队成员.") { StatusCode = 404 };
        }

        member.Role = (int)request.Role;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        await _teamService.RemoveRoleCacheAsync(request.TeamId, request.UserId, cancellationToken);
        await _eventPublisher.PublishTeamMemberUpdatedAsync(request.TeamId, request.UserId, (int)request.Role, cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
