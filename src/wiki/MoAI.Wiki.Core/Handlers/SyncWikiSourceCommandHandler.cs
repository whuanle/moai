using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Team.Services;
using MoAI.Wiki.Commands;
using MoAI.Wiki.Services;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="SyncWikiSourceCommand"/>
/// </summary>
public class SyncWikiSourceCommandHandler : IRequestHandler<SyncWikiSourceCommand, SyncWikiSourceCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly WikiSourceSyncService _syncService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncWikiSourceCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="syncService">外部源同步服务.</param>
    public SyncWikiSourceCommandHandler(
        DatabaseContext databaseContext,
        ITeamService teamService,
        WikiSourceSyncService syncService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _syncService = syncService;
    }

    /// <inheritdoc/>
    public async Task<SyncWikiSourceCommandResponse> Handle(SyncWikiSourceCommand request, CancellationToken cancellationToken)
    {
        var source = await _databaseContext.WikiSources
            .FirstOrDefaultAsync(x => x.Id == request.SourceId && x.WikiId == request.WikiId, cancellationToken);

        if (source == null)
        {
            throw new BusinessException("外部源不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(source.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以同步外部源.") { StatusCode = 403 };
        }

        if (!source.IsEnable)
        {
            throw new BusinessException("外部源已停用，请先启用后再同步.") { StatusCode = 409 };
        }

        return await _syncService.SyncAsync(source, request.Force, cancellationToken);
    }
}
