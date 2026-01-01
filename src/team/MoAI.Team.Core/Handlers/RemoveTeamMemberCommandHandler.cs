using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Commands;
using MoAI.Team.Models;

namespace MoAI.Team.Handlers;

/// <summary>
/// <inheritdoc cref="RemoveTeamMemberCommand"/>
/// </summary>
public class RemoveTeamMemberCommandHandler : IRequestHandler<RemoveTeamMemberCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoveTeamMemberCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public RemoveTeamMemberCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(RemoveTeamMemberCommand request, CancellationToken cancellationToken)
    {
        // 获取要移除的成员
        var membersToRemove = await _databaseContext.TeamUsers
            .Where(x => x.TeamId == request.TeamId && request.UserIds.Contains(x.UserId))
            .ToListAsync(cancellationToken);

        if (membersToRemove.Count == 0)
        {
            return EmptyCommandResponse.Default;
        }

        // 不能移除所有者
        if (membersToRemove.Any(x => x.Role == (int)TeamRole.Owner))
        {
            throw new BusinessException("不能移除团队所有者") { StatusCode = 403 };
        }

        _databaseContext.TeamUsers.RemoveRange(membersToRemove);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
