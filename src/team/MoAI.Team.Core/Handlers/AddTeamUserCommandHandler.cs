using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Commands;
using MoAI.Team.Services;

namespace MoAI.Team.Handlers;

/// <summary>
/// <inheritdoc cref="AddTeamUserCommand"/>
/// </summary>
public class AddTeamUserCommandHandler : IRequestHandler<AddTeamUserCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddTeamUserCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public AddTeamUserCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(AddTeamUserCommand request, CancellationToken cancellationToken)
    {
        var targetRole = await _databaseContext.TeamUsers
            .Where(x => x.TeamId == request.TeamId && x.UserId == request.UserId)
            .Select(x => (TeamRole?)x.Role)
            .FirstOrDefaultAsync(cancellationToken);

        if (targetRole != null)
        {
            throw new BusinessException("该用户已经是团队成员.") { StatusCode = 409 };
        }

        var targetUserExist = await _databaseContext.Users
            .AnyAsync(x => x.Id == request.UserId, cancellationToken);

        if (!targetUserExist)
        {
            throw new BusinessException("用户不存在.") { StatusCode = 404 };
        }

        _databaseContext.TeamUsers.Add(new TeamUserEntity
        {
            TeamId = (int)request.TeamId,
            UserId = request.UserId,
            Role = (int)request.Role,
        });

        await _databaseContext.SaveChangesAsync(cancellationToken);

        await _teamService.RemoveRoleCacheAsync(request.TeamId, request.UserId, cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
