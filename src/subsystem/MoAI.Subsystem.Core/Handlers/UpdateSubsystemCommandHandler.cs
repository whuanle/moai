using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Subsystem.Commands;
using MoAI.Subsystem.Core.Services;

namespace MoAI.Subsystem.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateSubsystemCommand"/>
/// </summary>
public class UpdateSubsystemCommandHandler : IRequestHandler<UpdateSubsystemCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly SubsystemOnlineService _onlineService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateSubsystemCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="onlineService">在线在场服务.</param>
    public UpdateSubsystemCommandHandler(DatabaseContext databaseContext, SubsystemOnlineService onlineService)
    {
        _databaseContext = databaseContext;
        _onlineService = onlineService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateSubsystemCommand request, CancellationToken cancellationToken)
    {
        var subsystem = await _databaseContext.Subsystems
            .FirstOrDefaultAsync(x => x.Id == request.SubsystemId, cancellationToken);
        if (subsystem == null)
        {
            throw new BusinessException("子系统不存在.") { StatusCode = 404 };
        }

        var teamIds = await SubsystemTeamsGuard.NormalizeAsync(request.TeamScope, request.TeamIds, _databaseContext, cancellationToken);

        subsystem.Name = request.Name;
        subsystem.Description = request.Description ?? string.Empty;
        subsystem.Status = request.Status;
        subsystem.TeamScope = request.TeamScope;
        subsystem.CallbackUrl = request.CallbackUrl ?? string.Empty;
        _databaseContext.Subsystems.Update(subsystem);

        // 授权团队列表整体覆盖：全员范围清空，指定范围按差集增删
        var existingAuths = await _databaseContext.SubsystemTeamAuths
            .Where(x => x.SubsystemId == subsystem.Id)
            .ToListAsync(cancellationToken);
        var removeAuths = request.TeamScope == SubsystemTeamScope.AllTeams
            ? existingAuths
            : existingAuths.Where(x => !teamIds.Contains(x.TeamId)).ToList();
        _databaseContext.SubsystemTeamAuths.RemoveRange(removeAuths);

        var addTeamIds = teamIds.Where(id => existingAuths.All(x => x.TeamId != id));
        foreach (var teamId in addTeamIds)
        {
            _databaseContext.SubsystemTeamAuths.Add(new SubsystemTeamAuthEntity
            {
                Id = Guid.CreateVersion7(),
                SubsystemId = subsystem.Id,
                TeamId = teamId,
            });
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        // 停用即时生效：清除在线在场键，机器面调用随中间件实时校验一并拒绝
        if (request.Status == SubsystemStatus.Disabled)
        {
            await _onlineService.RemoveAsync(subsystem.Id);
        }

        return EmptyCommandResponse.Default;
    }
}
