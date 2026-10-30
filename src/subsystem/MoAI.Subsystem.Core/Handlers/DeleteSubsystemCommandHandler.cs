using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Subsystem.Commands;
using MoAI.Subsystem.Core.Services;

namespace MoAI.Subsystem.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteSubsystemCommand"/>
/// </summary>
public class DeleteSubsystemCommandHandler : IRequestHandler<DeleteSubsystemCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly SubsystemOnlineService _onlineService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteSubsystemCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="onlineService">在线在场服务.</param>
    public DeleteSubsystemCommandHandler(DatabaseContext databaseContext, SubsystemOnlineService onlineService)
    {
        _databaseContext = databaseContext;
        _onlineService = onlineService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteSubsystemCommand request, CancellationToken cancellationToken)
    {
        var subsystem = await _databaseContext.Subsystems
            .FirstOrDefaultAsync(x => x.Id == request.SubsystemId, cancellationToken);
        if (subsystem == null)
        {
            throw new BusinessException("子系统不存在.") { StatusCode = 404 };
        }

        _databaseContext.Subsystems.Remove(subsystem);

        var auths = await _databaseContext.SubsystemTeamAuths
            .Where(x => x.SubsystemId == subsystem.Id)
            .ToListAsync(cancellationToken);
        _databaseContext.SubsystemTeamAuths.RemoveRange(auths);

        await _databaseContext.SaveChangesAsync(cancellationToken);
        await _onlineService.RemoveAsync(subsystem.Id);
        return EmptyCommandResponse.Default;
    }
}
