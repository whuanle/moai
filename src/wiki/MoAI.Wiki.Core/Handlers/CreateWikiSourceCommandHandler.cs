using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Hangfire.Services;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;
using MoAI.Wiki.Commands;
using MoAI.Wiki.Jobs;
using MoAI.Wiki.Models;
using MoAI.Wiki.Services;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="CreateWikiSourceCommand"/>
/// </summary>
public class CreateWikiSourceCommandHandler : IRequestHandler<CreateWikiSourceCommand, SimpleGuid>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly WikiSourceBindingService _bindingService;
    private readonly IRecurringJobService _recurringJobService;
    private readonly WikiSourceSyncService _syncService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateWikiSourceCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="bindingService">飞书应用绑定服务.</param>
    /// <param name="recurringJobService">定时任务服务.</param>
    /// <param name="syncService">外部源同步服务.</param>
    public CreateWikiSourceCommandHandler(
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
    public async Task<SimpleGuid> Handle(CreateWikiSourceCommand request, CancellationToken cancellationToken)
    {
        var wiki = await _databaseContext.Wikis
            .FirstOrDefaultAsync(x => x.Id == request.WikiId && x.IsDeleted == 0, cancellationToken);

        if (wiki == null)
        {
            throw new BusinessException("知识库不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(wiki.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以管理外部源.") { StatusCode = 403 };
        }

        var duplicated = await _databaseContext.WikiSources
            .AnyAsync(x => x.WikiId == wiki.Id && x.Name == request.Name, cancellationToken);
        if (duplicated)
        {
            throw new BusinessException("该知识库下已存在同名外部源.") { StatusCode = 409 };
        }

        var isFeishu = request.SourceType == WikiSourceType.FeishuDoc;

        // 飞书文档源需先确定使用的飞书应用连接（已有连接或新建连接）；爬虫源无需绑定飞书应用
        var feishuAppId = Guid.Empty;
        if (isFeishu)
        {
            feishuAppId = await _bindingService.ResolveAsync(
                wiki.TeamId,
                request.ContextUserId,
                request.ContextUserType,
                request.FeishuAppId,
                request.NewAppName,
                request.NewAppId,
                request.NewAppSecret,
                request.NewAppDomain,
                cancellationToken);
        }

        var sourceId = Guid.CreateVersion7();
        var source = new WikiSourceEntity
        {
            Id = sourceId,
            WikiId = wiki.Id,
            TeamId = wiki.TeamId,
            SourceType = (int)request.SourceType,
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            Config = isFeishu
                ? WikiSourceConfigJson.SerializeFeishu(new WikiSourceFeishuConfig
                {
                    FeishuAppId = feishuAppId,
                    NodeToken = request.NodeToken,
                    IncludeSubNodes = request.IncludeSubNodes,
                    MaxDepth = request.MaxDepth,
                    MaxDocuments = request.MaxDocuments,
                })
                : WikiSourceConfigJson.SerializeCrawler(request.Crawler ?? new WikiSourceCrawlerConfig()),
            WorkflowConfig = request.Workflow == null ? string.Empty : WikiWorkflowConfigJson.Serialize(request.Workflow),
            IsEnable = request.IsEnable,
            Cron = request.Cron ?? string.Empty,
            IsEventSubscription = isFeishu && request.IsEventSubscription,
            LastSyncStatus = (int)WikiSourceSyncStatus.None,
            LastSyncMessage = string.Empty,
        };

        await _databaseContext.WikiSources.AddAsync(source, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        if (isFeishu)
        {
            try
            {
                await _bindingService.BindAsync(feishuAppId, sourceId, request.ContextUserId, request.ContextUserType, cancellationToken);
            }
            catch (Exception)
            {
                // 绑定失败则回退已创建的外部源，避免留下无法接收事件的孤儿记录
                _databaseContext.WikiSources.Remove(source);
                await _databaseContext.SaveChangesAsync(cancellationToken);
                throw;
            }
        }

        if (!string.IsNullOrWhiteSpace(source.Cron))
        {
            await _recurringJobService.AddOrUpdateRecurringJobAsync<WikiSourceSyncJobCommand, WikiSourceSyncJobParams>(
                WikiSourceDefaults.BuildSyncJobKey(sourceId),
                source.Cron,
                new WikiSourceSyncJobParams { SourceId = sourceId });
        }

        // 创建后立刻拉取一次，让用户马上看到文档清单；失败不阻断创建，状态记入外部源
        try
        {
            await _syncService.SyncAsync(source, false, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 同步服务已写入失败状态，此处仅避免创建接口整体失败
        }

        return new SimpleGuid { Value = sourceId };
    }
}
