using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Models;
using MoAI.Team.Commands;
using MoAI.Team.Queries.Responses;
using MoAI.Team.Services;

namespace MoAI.Team.Handlers;

/// <summary>
/// <inheritdoc cref="AddTeamUsersCommand"/>
/// </summary>
public class AddTeamUsersCommandHandler : IRequestHandler<AddTeamUsersCommand, AddTeamUsersCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddTeamUsersCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public AddTeamUsersCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<AddTeamUsersCommandResponse> Handle(AddTeamUsersCommand request, CancellationToken cancellationToken)
    {
        var userIds = request.UserIds.Distinct().ToList();

        var existingUserIds = await _databaseContext.Users
            .Where(x => userIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToHashSetAsync(cancellationToken);

        var memberUserIds = await _databaseContext.TeamUsers
            .Where(x => x.TeamId == request.TeamId && userIds.Contains(x.UserId))
            .Select(x => x.UserId)
            .ToHashSetAsync(cancellationToken);

        var addIds = userIds.Where(x => existingUserIds.Contains(x) && !memberUserIds.Contains(x)).ToList();
        var skippedUserIds = userIds.Where(memberUserIds.Contains).ToList();
        var notFoundUserIds = userIds.Where(x => !existingUserIds.Contains(x)).ToList();

        if (addIds.Count > 0)
        {
            _databaseContext.TeamUsers.AddRange(addIds.Select(userId => new TeamUserEntity
            {
                TeamId = (int)request.TeamId,
                UserId = userId,
                Role = (int)request.Role,
            }));

            await _databaseContext.SaveChangesAsync(cancellationToken);

            foreach (var userId in addIds)
            {
                await _teamService.RemoveRoleCacheAsync(request.TeamId, userId, cancellationToken);
            }
        }

        return new AddTeamUsersCommandResponse
        {
            AddedCount = addIds.Count,
            SkippedUserIds = skippedUserIds,
            NotFoundUserIds = notFoundUserIds,
        };
    }
}
