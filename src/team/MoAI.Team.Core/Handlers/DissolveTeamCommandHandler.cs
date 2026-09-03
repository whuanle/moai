using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
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
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DissolveTeamCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public DissolveTeamCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IUserContextProvider userContextProvider)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userContextProvider = userContextProvider;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DissolveTeamCommand request, CancellationToken cancellationToken)
    {
        var userId = _userContextProvider.GetUserContext().UserId;
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, userId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole != TeamRole.Owner)
        {
            throw new BusinessException("只有团队所有者可以解散团队.") { StatusCode = 403 };
        }

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

        return EmptyCommandResponse.Default;
    }
}
