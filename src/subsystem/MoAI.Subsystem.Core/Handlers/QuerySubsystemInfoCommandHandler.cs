using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Subsystem.Core.Services;
using MoAI.Subsystem.Queries;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Handlers;

/// <summary>
/// <inheritdoc cref="QuerySubsystemInfoCommand"/>
/// </summary>
public class QuerySubsystemInfoCommandHandler : IRequestHandler<QuerySubsystemInfoCommand, QuerySubsystemInfoCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly SubsystemOnlineService _onlineService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuerySubsystemInfoCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="onlineService">在线在场服务.</param>
    public QuerySubsystemInfoCommandHandler(DatabaseContext databaseContext, SubsystemOnlineService onlineService)
    {
        _databaseContext = databaseContext;
        _onlineService = onlineService;
    }

    /// <inheritdoc/>
    public async Task<QuerySubsystemInfoCommandResponse> Handle(QuerySubsystemInfoCommand request, CancellationToken cancellationToken)
    {
        var subsystem = await _databaseContext.Subsystems
            .FirstOrDefaultAsync(x => x.Id == request.SubsystemId, cancellationToken);
        if (subsystem == null)
        {
            throw new BusinessException("子系统不存在.") { StatusCode = 404 };
        }

        var authorizedTeamCount = await _databaseContext.SubsystemTeamAuths
            .CountAsync(x => x.SubsystemId == subsystem.Id, cancellationToken);
        var online = await _onlineService.IsOnlineAsync(subsystem.Id);

        return new QuerySubsystemInfoCommandResponse
        {
            SubsystemId = subsystem.Id,
            Key = subsystem.Key,
            Name = subsystem.Name,
            Status = subsystem.Status,
            TeamScope = subsystem.TeamScope,
            AuthorizedTeamCount = authorizedTeamCount,
            Online = online,
        };
    }
}
