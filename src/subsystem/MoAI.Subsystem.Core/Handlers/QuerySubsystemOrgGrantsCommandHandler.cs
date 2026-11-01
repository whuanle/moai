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
/// <inheritdoc cref="QuerySubsystemOrgGrantsCommand"/>
/// </summary>
public class QuerySubsystemOrgGrantsCommandHandler : IRequestHandler<QuerySubsystemOrgGrantsCommand, QuerySubsystemOrgGrantsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IOrgGrantService _orgGrantService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuerySubsystemOrgGrantsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="orgGrantService">统一组织授权领域服务.</param>
    public QuerySubsystemOrgGrantsCommandHandler(DatabaseContext databaseContext, IOrgGrantService orgGrantService)
    {
        _databaseContext = databaseContext;
        _orgGrantService = orgGrantService;
    }

    /// <inheritdoc/>
    public async Task<QuerySubsystemOrgGrantsCommandResponse> Handle(QuerySubsystemOrgGrantsCommand request, CancellationToken cancellationToken)
    {
        // 组织授权是子系统专属配置：应用接入行不存在该维度，404
        var subsystem = await _databaseContext.AccessApps
            .Where(x => x.Level == AccessAppLevel.System && x.AppType == AccessAppType.Subsystem)
            .FirstOrDefaultAsync(x => x.Id == request.SubsystemId, cancellationToken)
            ?? throw new BusinessException("子系统不存在.") { StatusCode = 404 };

        var grants = await _orgGrantService.GetGrantsAsync(OrgGrantResourceType.Subsystem, subsystem.Id, cancellationToken);
        return new QuerySubsystemOrgGrantsCommandResponse
        {
            Items = grants.Select(x => new SubsystemOrgGrantInfoItem
            {
                OrgRoleId = x.OrgRoleId,
                RoleName = x.RoleName,
                RolePath = x.RolePath,
                Scope = x.Scope,
            }).ToList(),
        };
    }
}
