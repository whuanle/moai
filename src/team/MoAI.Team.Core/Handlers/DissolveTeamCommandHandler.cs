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
/// <inheritdoc cref="DissolveTeamCommand"/>
/// </summary>
public class DissolveTeamCommandHandler : IRequestHandler<DissolveTeamCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DissolveTeamCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public DissolveTeamCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DissolveTeamCommand request, CancellationToken cancellationToken)
    {
        var team = await _databaseContext.Teams
            .FirstOrDefaultAsync(x => x.Id == request.TeamId, cancellationToken);

        if (team == null)
        {
            throw new BusinessException("团队不存在.") { StatusCode = 404 };
        }

        // 软删除团队与全部成员关系，实体实现 IFullAudited，框架将删除转换为软删除并记录操作人
        var members = await _databaseContext.TeamUsers
            .Where(x => x.TeamId == request.TeamId)
            .ToListAsync(cancellationToken);

        _databaseContext.TeamUsers.RemoveRange(members);
        _databaseContext.Teams.Remove(team);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        // 移除全部成员在团队中的角色缓存
        foreach (var member in members)
        {
            await _teamService.RemoveRoleCacheAsync(request.TeamId, member.UserId, cancellationToken);
        }

        return EmptyCommandResponse.Default;
    }
}
