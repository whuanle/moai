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
/// <inheritdoc cref="UpdateWikiSourceCommand"/>
/// </summary>
public class UpdateWikiSourceCommandHandler : IRequestHandler<UpdateWikiSourceCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly WikiSourceBindingService _bindingService;
    private readonly IRecurringJobService _recurringJobService;
    private readonly WikiSourceSyncService _syncService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateWikiSourceCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="bindingService">飞书应用绑定服务.</param>
    /// <param name="recurringJobService">定时任务服务.</param>
    /// <param name="syncService">外部源同步服务.</param>
    public UpdateWikiSourceCommandHandler(
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
    public async Task<EmptyCommandResponse> Handle(UpdateWikiSourceCommand request, CancellationToken cancellationToken)
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

        if (!string.IsNullOrWhiteSpace(request.Name) && !string.Equals(source.Name, request.Name, StringComparison.Ordinal))
        {
            var duplicated = await _databaseContext.WikiSources
                .AnyAsync(x => x.WikiId == source.WikiId && x.Name == request.Name && x.Id != source.Id, cancellationToken);
            if (duplicated)
            {
                throw new BusinessException("该知识库下已存在同名外部源.") { StatusCode = 409 };
            }

            source.Name = request.Name;
        }

        if (request.Description != null)
        {
            source.Description = request.Description;
        }

        if (request.IsEnable.HasValue)
        {
            source.IsEnable = request.IsEnable.Value;
        }

        source.WorkflowConfig = request.Workflow == null ? string.Empty : WikiWorkflowConfigJson.Serialize(request.Workflow);

        // 外部源类型创建后不可变更，分支一律以库里的类型为准，避免请求体缺省值误判
        if (source.SourceType == (int)WikiSourceType.FeishuDoc)
        {
            await ApplyFeishuAsync(source, request, cancellationToken);
        }
        else if (request.Crawler != null)
        {
            source.Config = WikiSourceConfigJson.SerializeCrawler(request.Crawler);
        }

        var jobKey = WikiSourceDefaults.BuildSyncJobKey(source.Id);
        if (request.Cron != null)
        {
            source.Cron = request.Cron;
            if (string.IsNullOrWhiteSpace(source.Cron))
            {
                await _recurringJobService.RemoveRecurringJobAsync(jobKey);
            }
            else
            {
                await _recurringJobService.AddOrUpdateRecurringJobAsync<WikiSourceSyncJobCommand, WikiSourceSyncJobParams>(
                    jobKey,
                    source.Cron,
                    new WikiSourceSyncJobParams { SourceId = source.Id });
            }
        }

        if (request.IsEventSubscription.HasValue && request.IsEventSubscription.Value != source.IsEventSubscription)
        {
            source.IsEventSubscription = request.IsEventSubscription.Value;

            if (source.IsEventSubscription)
            {
                // 开启订阅前补拉一次，保证已同步文档都有可用于事件匹配的文档 token
                await _syncService.SyncAsync(source, false, cancellationToken);
                await _syncService.SubscribeDocumentsAsync(source, cancellationToken);
            }
            else
            {
                await _syncService.UnsubscribeDocumentsAsync(source, cancellationToken);
            }
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }

    /// <summary>
    /// 应用飞书文档源的变更：节点参数与飞书应用绑定（改绑时先解除旧绑定）.
    /// </summary>
    /// <param name="source">外部源实体.</param>
    /// <param name="request">更新请求.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    private async Task ApplyFeishuAsync(WikiSourceEntity source, UpdateWikiSourceCommand request, CancellationToken cancellationToken)
    {
        var config = WikiSourceConfigJson.DeserializeFeishu(source.Config) ?? new WikiSourceFeishuConfig();
        var rebinding = request.FeishuAppId.HasValue || !string.IsNullOrWhiteSpace(request.NewAppId);

        if (rebinding)
        {
            var feishuAppId = await _bindingService.ResolveAsync(
                source.TeamId,
                request.ContextUserId,
                request.ContextUserType,
                request.FeishuAppId,
                request.NewAppName,
                request.NewAppId,
                request.NewAppSecret,
                request.NewAppDomain,
                cancellationToken);

            if (config.FeishuAppId != Guid.Empty && config.FeishuAppId != feishuAppId)
            {
                await _bindingService.UnbindAsync(config.FeishuAppId, source.Id, request.ContextUserId, request.ContextUserType, cancellationToken);
            }

            if (config.FeishuAppId != feishuAppId)
            {
                await _bindingService.BindAsync(feishuAppId, source.Id, request.ContextUserId, request.ContextUserType, cancellationToken);
                config = config with { FeishuAppId = feishuAppId, SpaceId = null };
            }
        }

        if (!string.IsNullOrWhiteSpace(request.NodeToken) && !string.Equals(config.NodeToken, request.NodeToken, StringComparison.Ordinal))
        {
            config = config with { NodeToken = request.NodeToken, SpaceId = null };
        }

        if (request.IncludeSubNodes.HasValue)
        {
            config = config with { IncludeSubNodes = request.IncludeSubNodes.Value };
        }

        if (request.MaxDepth.HasValue)
        {
            config = config with { MaxDepth = request.MaxDepth.Value };
        }

        if (request.MaxDocuments.HasValue)
        {
            config = config with { MaxDocuments = request.MaxDocuments.Value };
        }

        source.Config = WikiSourceConfigJson.SerializeFeishu(config);
    }
}
