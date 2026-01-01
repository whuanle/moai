using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Commands;
using MoAI.Team.Models;

namespace MoAI.Team.Handlers;

/// <summary>
/// <inheritdoc cref="InviteTeamMemberCommand"/>
/// </summary>
public class InviteTeamMemberCommandHandler : IRequestHandler<InviteTeamMemberCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="InviteTeamMemberCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public InviteTeamMemberCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(InviteTeamMemberCommand request, CancellationToken cancellationToken)
    {
        // 检查团队是否存在
        var teamExists = await _databaseContext.Teams.AnyAsync(x => x.Id == request.TeamId, cancellationToken);
        if (!teamExists)
        {
            throw new BusinessException("团队不存在") { StatusCode = 404 };
        }

        // 收集用户ID
        var userIds = new HashSet<int>(request.UserIds);

        if (request.UserNames.Count > 0)
        {
            var users = await _databaseContext.Users
                .Where(x => request.UserNames.Contains(x.UserName))
                .Select(x => x.Id)
                .ToArrayAsync(cancellationToken);

            if (users.Length != request.UserNames.Count)
            {
                throw new BusinessException("部分用户账号不存在");
            }

            foreach (var userId in users)
            {
                userIds.Add(userId);
            }
        }

        // 获取已存在的成员
        var existingMembers = await _databaseContext.TeamUsers
            .Where(x => x.TeamId == request.TeamId)
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);

        // 移除已存在的成员
        foreach (var existingMember in existingMembers)
        {
            userIds.Remove(existingMember);
        }

        if (userIds.Count == 0)
        {
            return EmptyCommandResponse.Default;
        }

        // 添加新成员
        var newMembers = userIds.Select(userId => new TeamUserEntity
        {
            TeamId = request.TeamId,
            UserId = userId,
            Role = (int)TeamRole.Collaborator
        }).ToList();

        await _databaseContext.TeamUsers.AddRangeAsync(newMembers, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
