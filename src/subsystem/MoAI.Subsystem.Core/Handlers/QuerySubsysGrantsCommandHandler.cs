using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Org.Services;
using MoAI.Subsystem.Queries;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Handlers;

/// <summary>
/// <inheritdoc cref="QuerySubsysGrantsCommand"/>
/// </summary>
public class QuerySubsysGrantsCommandHandler : IRequestHandler<QuerySubsysGrantsCommand, QuerySubsysGrantsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IOrgGrantService _orgGrantService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuerySubsysGrantsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="orgGrantService">组织授权服务.</param>
    public QuerySubsysGrantsCommandHandler(DatabaseContext databaseContext, IOrgGrantService orgGrantService)
    {
        _databaseContext = databaseContext;
        _orgGrantService = orgGrantService;
    }

    /// <inheritdoc/>
    public async Task<QuerySubsysGrantsCommandResponse> Handle(QuerySubsysGrantsCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.AccessApps
            .AsNoTracking()
            .Where(x => x.Id == request.SubsystemId && x.Level == AccessAppLevel.System)
            .Select(x => new { x.TeamScope })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessException("接入不存在.") { StatusCode = 404 };

        var teamIds = await _databaseContext.SubsystemTeamAuths
            .AsNoTracking()
            .Where(x => x.SubsystemId == request.SubsystemId)
            .Select(x => (long)x.TeamId)
            .ToListAsync(cancellationToken);

        var orgGrants = await _orgGrantService.GetGrantsAsync(OrgGrantResourceType.Subsystem, request.SubsystemId, cancellationToken);

        return new QuerySubsysGrantsCommandResponse
        {
            TeamScope = app.TeamScope,
            TeamIds = teamIds,
            OrgGrants = orgGrants.Select(x => new QuerySubsysOrgGrantItem
            {
                OrgRoleId = x.OrgRoleId,
                Scope = x.Scope,
            }).ToList(),
        };
    }
}
