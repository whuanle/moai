using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.App.Models;
using MoAI.Database;
using MoAI.Database.Aggregates;
using MoAI.Infra;
using MoAI.Infra.Exceptions;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="QueryExternalAccessPointCommand"/>
/// </summary>
public class QueryExternalAccessPointCommandHandler : IRequestHandler<QueryExternalAccessPointCommand, ExternalAccessPointResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryExternalAccessPointCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="systemOptions">系统配置.</param>
    public QueryExternalAccessPointCommandHandler(DatabaseContext databaseContext, SystemOptions systemOptions)
    {
        _databaseContext = databaseContext;
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public async Task<ExternalAccessPointResponse> Handle(QueryExternalAccessPointCommand request, CancellationToken cancellationToken)
    {
        var rows = await _databaseContext.Apps
            .Where(x => x.Id == request.AppId)
            .Select(x => new
            {
                x.Name,
                x.Avatar,
                x.IsExternal,
                x.IsDisable,
                x.PublishStatus
            })
            .FirstOrDefaultAsync(cancellationToken);

        // 应用不存在按不存在处理；内部应用未发布/禁用同样按不存在处理，不泄露存在性（外部应用保持原口径回 Enabled=false）
        if (rows == null || (!rows.IsExternal && (rows.IsDisable || rows.PublishStatus != 1)))
        {
            throw new BusinessException("应用不存在.") { StatusCode = 404 };
        }

        var config = await _databaseContext.AppAccessPoints
            .Where(x => x.AppId == request.AppId)
            .Select(x => new
            {
                x.Title,
                x.Subtitle,
                x.Placeholder,
                x.PrimaryColor,
                x.Position,
                x.DisplayMode,
                x.LauncherText,
                x.Avatar,
                x.PanelWidth,
                x.PanelHeight,
                x.DefaultOpen,
                x.Enabled
            })
            .FirstOrDefaultAsync(cancellationToken);

        // 开场白与外部对话同源：已发布应用取发布快照，未发布/快照无效回退实时草稿
        var agentConfig = await _databaseContext.AppAgentConfigs
            .Where(x => x.AppId == request.AppId)
            .FirstOrDefaultAsync(cancellationToken);
        string? openingStatement = null;
        if (agentConfig != null)
        {
            var effective = AppAgentConfigSnapshot.ResolveEffectiveConfig(
                new Database.Entities.AppEntity { Id = request.AppId, PublishStatus = rows.PublishStatus },
                agentConfig,
                preferPublished: true);
            if (effective.OpeningStatementEnabled && !string.IsNullOrWhiteSpace(effective.OpeningStatement))
            {
                openingStatement = effective.OpeningStatement;
            }
        }

        var server = _systemOptions.Server.TrimEnd('/');
        var avatarObjectKey = config?.Avatar;
        if (string.IsNullOrEmpty(avatarObjectKey))
        {
            avatarObjectKey = rows.Avatar;
        }

        return new ExternalAccessPointResponse
        {
            AppName = rows.Name,
            AvatarUrl = string.IsNullOrEmpty(avatarObjectKey) ? string.Empty : $"{server}/static/{avatarObjectKey.TrimStart('/')}",
            Title = string.IsNullOrEmpty(config?.Title) ? rows.Name : config!.Title!,
            Subtitle = config?.Subtitle,
            Placeholder = config?.Placeholder,
            PrimaryColor = config?.PrimaryColor,
            Position = AccessPointPositionText.Normalize(config?.Position),
            DisplayMode = AccessPointDisplayModeText.Normalize(config?.DisplayMode),
            LauncherText = config?.LauncherText,
            PanelWidth = config?.PanelWidth ?? 380,
            PanelHeight = config?.PanelHeight ?? 560,
            DefaultOpen = config?.DefaultOpen ?? false,
            OpeningStatement = openingStatement,
            AuthMode = rows.IsExternal ? "open" : "token",
            Endpoints = new ExternalAccessPointEndpointsResponse
            {
                Chat = $"{server}/api/teamres/agent/{request.AppId}/chat",
                Sessions = $"{server}/api/teamres/agent/{request.AppId}/session",
                Messages = $"{server}/api/teamres/session/{{sessionId}}/messages",
                Token = rows.IsExternal
                    ? string.Empty
                    : $"{server}/api/subsys/system/app-tokens",
            },
            Enabled = !rows.IsDisable && rows.PublishStatus == 1
                && (rows.IsExternal ? (config?.Enabled ?? true) : (config != null && config.Enabled)),
        };
    }
}
