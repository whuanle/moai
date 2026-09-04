using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Commands;
using MoAI.Team.Services;

namespace MoAI.Team.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateTeamOwnerCommand"/>
/// </summary>
public class UpdateTeamOwnerCommandHandler : IRequestHandler<UpdateTeamOwnerCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTeamOwnerCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public UpdateTeamOwnerCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateTeamOwnerCommand request, CancellationToken cancellationToken)
    {
        if (request.UserId == request.ContextUserId)
        {
            throw new BusinessException("不能转让给自己.") { StatusCode = 400 };
        }

        var target = await _databaseContext.TeamUsers
            .FirstOrDefaultAsync(x => x.TeamId == request.TeamId && x.UserId == request.UserId, cancellationToken);

        if (target == null)
        {
            throw new BusinessException("目标用户不是团队成员.") { StatusCode = 404 };
        }

        // 原所有者降为 Admin，目标成员升为 Owner
        var currentOwner = await _databaseContext.TeamUsers
            .FirstAsync(x => x.TeamId == request.TeamId && x.UserId == request.ContextUserId, cancellationToken);

        currentOwner.Role = (int)TeamRole.Admin;
        target.Role = (int)TeamRole.Owner;

        await _databaseContext.SaveChangesAsync(cancellationToken);

        await _teamService.RemoveRoleCacheAsync(request.TeamId, request.ContextUserId, cancellationToken);
        await _teamService.RemoveRoleCacheAsync(request.TeamId, request.UserId, cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
