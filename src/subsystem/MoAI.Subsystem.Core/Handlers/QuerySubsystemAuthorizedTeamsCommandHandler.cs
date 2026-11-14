using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Subsystem.Queries;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Handlers;

/// <summary>
/// <inheritdoc cref="QuerySubsystemAuthorizedTeamsCommand"/>
/// </summary>
public class QuerySubsystemAuthorizedTeamsCommandHandler : IRequestHandler<QuerySubsystemAuthorizedTeamsCommand, QuerySubsystemAuthorizedTeamsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuerySubsystemAuthorizedTeamsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QuerySubsystemAuthorizedTeamsCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QuerySubsystemAuthorizedTeamsCommandResponse> Handle(QuerySubsystemAuthorizedTeamsCommand request, CancellationToken cancellationToken)
    {
        // 团队授权是子系统专属配置：应用接入行不存在该维度，404
        var subsystem = await _databaseContext.AccessApps
            .Where(x => x.Level == AccessAppLevel.System && x.AppType == AccessAppType.Subsystem)
            .FirstOrDefaultAsync(x => x.Id == request.SubsystemId, cancellationToken)
            ?? throw new BusinessException("子系统不存在.") { StatusCode = 404 };

        var teamIds = await _databaseContext.SubsystemTeamAuths
            .Where(x => x.SubsystemId == subsystem.Id)
            .OrderBy(x => x.TeamId)
            .Select(x => x.TeamId)
            .ToListAsync(cancellationToken);
        return new QuerySubsystemAuthorizedTeamsCommandResponse { TeamIds = teamIds };
    }
}
