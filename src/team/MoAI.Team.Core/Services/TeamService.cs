using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Team.Services;

namespace MoAI.Team.Services;

/// <summary>
/// 团队领域服务实现.
/// </summary>
public class TeamService : ITeamService
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public TeamService(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<TeamRole?> GetMyRoleAsync(long teamId, long userId, CancellationToken cancellationToken = default)
    {
        var role = await _databaseContext.TeamUsers
            .Where(x => x.TeamId == teamId && x.UserId == userId)
            .Select(x => (TeamRole?)x.Role)
            .FirstOrDefaultAsync(cancellationToken);

        return role;
    }
}
