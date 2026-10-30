using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Subsystem.Queries;
using MoAI.Subsystem.Queries.Responses;
using MoAI.Team.Services;

namespace MoAI.Subsystem.Handlers;

/// <summary>
/// <inheritdoc cref="QueryTeamAvailableSubsystemsCommand"/>
/// </summary>
public class QueryTeamAvailableSubsystemsCommandHandler : IRequestHandler<QueryTeamAvailableSubsystemsCommand, QueryTeamAvailableSubsystemsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamAvailableSubsystemsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public QueryTeamAvailableSubsystemsCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<QueryTeamAvailableSubsystemsCommandResponse> Handle(QueryTeamAvailableSubsystemsCommand request, CancellationToken cancellationToken)
    {
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        var subsystems = await _databaseContext.Subsystems
            .Where(x => x.Status == SubsystemStatus.Enabled)
            .ToListAsync(cancellationToken);

        var authorizedSubsystemIds = await _databaseContext.SubsystemTeamAuths
            .Where(x => x.TeamId == request.TeamId)
            .Select(x => x.SubsystemId)
            .ToListAsync(cancellationToken);

        var items = subsystems
            .OrderBy(x => x.CreateTime)
            .Where(x => x.TeamScope == SubsystemTeamScope.AllTeams || authorizedSubsystemIds.Contains(x.Id))
            .Select(x => new QueryTeamAvailableSubsystemsCommandResponseItem
            {
                SubsystemId = x.Id,
                Key = x.Key,
                Name = x.Name,
                Description = x.Description,
            })
            .ToList();

        return new QueryTeamAvailableSubsystemsCommandResponse { Items = items };
    }
}
