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
/// <inheritdoc cref="AdminTransferTeamOwnerCommand"/>
/// </summary>
public class AdminTransferTeamOwnerCommandHandler : IRequestHandler<AdminTransferTeamOwnerCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminTransferTeamOwnerCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public AdminTransferTeamOwnerCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(AdminTransferTeamOwnerCommand request, CancellationToken cancellationToken)
    {
        var teamExist = await _databaseContext.Teams
            .AnyAsync(x => x.Id == request.TeamId, cancellationToken);

        if (!teamExist)
        {
            throw new BusinessException("团队不存在.") { StatusCode = 404 };
        }

        var userExist = await _databaseContext.Users
            .AnyAsync(x => x.Id == request.UserId, cancellationToken);

        if (!userExist)
        {
            throw new BusinessException("目标用户不存在.") { StatusCode = 404 };
        }

        var members = await _databaseContext.TeamUsers
            .Where(x => x.TeamId == request.TeamId)
            .ToListAsync(cancellationToken);

        var target = members.FirstOrDefault(x => x.UserId == request.UserId);
        if (target != null && target.Role == (int)TeamRole.Owner)
        {
            throw new BusinessException("该用户已经是团队负责人.") { StatusCode = 400 };
        }

        // 原负责人降为管理员；目标用户非团队成员时直接加入并成为负责人
        var currentOwner = members.FirstOrDefault(x => x.Role == (int)TeamRole.Owner);
        if (currentOwner != null)
        {
            currentOwner.Role = (int)TeamRole.Admin;
        }

        if (target == null)
        {
            _databaseContext.TeamUsers.Add(new TeamUserEntity
            {
                TeamId = (int)request.TeamId,
                UserId = request.UserId,
                Role = (int)TeamRole.Owner,
            });
        }
        else
        {
            target.Role = (int)TeamRole.Owner;
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        if (currentOwner != null)
        {
            await _teamService.RemoveRoleCacheAsync(request.TeamId, currentOwner.UserId, cancellationToken);
        }

        await _teamService.RemoveRoleCacheAsync(request.TeamId, request.UserId, cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
