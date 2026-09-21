using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Hangfire.Services;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;
using MoAI.Wiki.Commands;
using MoAI.Wiki.Models;
using MoAI.Wiki.Services;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteWikiSourceCommand"/>
/// </summary>
public class DeleteWikiSourceCommandHandler : IRequestHandler<DeleteWikiSourceCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly WikiSourceBindingService _bindingService;
    private readonly IRecurringJobService _recurringJobService;
    private readonly WikiSourceSyncService _syncService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWikiSourceCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="bindingService">飞书应用绑定服务.</param>
    /// <param name="recurringJobService">定时任务服务.</param>
    /// <param name="syncService">外部源同步服务.</param>
    public DeleteWikiSourceCommandHandler(
        DatabaseContext databaseContext,
        ITeamService teamService,
        WikiSourceBindingService bindingService,
        IRecurringJobService recurringJobService,
        WikiSourceSyncService syncService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _bindingService = bindingService;
        _recurringJobService = recurringJobService;
        _syncService = syncService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteWikiSourceCommand request, CancellationToken cancellationToken)
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
            throw new BusinessException("只有团队管理员可以管理外部源.") { StatusCode = 403 };
        }

        var config = WikiSourceConfigJson.DeserializeFeishu(source.Config);

        // 先退订云文档事件、解绑渠道、移除定时任务，再落库软删除，避免残留后台行为
        await _syncService.UnsubscribeDocumentsAsync(source, cancellationToken);

        if (config != null && config.FeishuAppId != Guid.Empty)
        {
            await _bindingService.UnbindAsync(config.FeishuAppId, source.Id, request.ContextUserId, request.ContextUserType, cancellationToken);
        }

        await _recurringJobService.RemoveRecurringJobAsync(WikiSourceDefaults.BuildSyncJobKey(source.Id));

        var documents = await _databaseContext.WikiSourceDocuments
            .Where(x => x.SourceId == source.Id)
            .ToArrayAsync(cancellationToken);

        if (documents.Length > 0)
        {
            _databaseContext.WikiSourceDocuments.RemoveRange(documents);
        }

        _databaseContext.WikiSources.Remove(source);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
