using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Org.Services;
using MoAI.Subsystem.Commands;

namespace MoAI.Subsystem.Handlers;

/// <summary>
/// <inheritdoc cref="SetSubsystemOrgGrantsCommand"/>
/// </summary>
public class SetSubsystemOrgGrantsCommandHandler : IRequestHandler<SetSubsystemOrgGrantsCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IOrgGrantService _orgGrantService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetSubsystemOrgGrantsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="orgGrantService">统一组织授权领域服务.</param>
    public SetSubsystemOrgGrantsCommandHandler(DatabaseContext databaseContext, IOrgGrantService orgGrantService)
    {
        _databaseContext = databaseContext;
        _orgGrantService = orgGrantService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(SetSubsystemOrgGrantsCommand request, CancellationToken cancellationToken)
    {
        var subsystem = await _databaseContext.AccessApps
            .Where(x => x.Level == AccessAppLevel.System && x.AppType == AccessAppType.Subsystem)
            .FirstOrDefaultAsync(x => x.Id == request.SubsystemId, cancellationToken);
        if (subsystem == null)
        {
            throw new BusinessException("子系统不存在.") { StatusCode = 404 };
        }

        var grants = request.Grants
            .Select(x => new OrgGrantItem { OrgRoleId = x.OrgRoleId, Scope = x.Scope })
            .ToList();
        await _orgGrantService.ReplaceGrantsAsync(OrgGrantResourceType.Subsystem, subsystem.Id, grants, cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
