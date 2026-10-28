using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.Database;
using MoAI.Database.Aggregates;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteAppCommand"/>
/// </summary>
public class DeleteAppCommandHandler : IRequestHandler<DeleteAppCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public DeleteAppCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteAppCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken)
            ?? throw new BusinessException("应用不存在.") { StatusCode = 404 };

        // 发布快照与草稿都可能继续调用目标应用，不能静默留下失效的工具或流程节点。
        var agentReferences = await (from config in _databaseContext.AppAgentConfigs.AsNoTracking()
                                     join owner in _databaseContext.Apps on config.AppId equals owner.Id
                                     where owner.Id != app.Id && owner.TeamId == app.TeamId
                                     select new { owner.Name, config.WorkflowApps, config.PublishedConfig })
            .ToListAsync(cancellationToken);
        foreach (var reference in agentReferences)
        {
            if (AppAgentConfigJson.ParseGuidList(reference.WorkflowApps).Contains(app.Id)
                || AppAgentConfigJson.ParseGuidList(AppAgentConfigSnapshot.TryParse(reference.PublishedConfig)?.WorkflowApps).Contains(app.Id))
            {
                throw Referenced(reference.Name);
            }
        }

        var workflowReferences = await (from config in _databaseContext.AppWorkflowConfigs.AsNoTracking()
                                        join owner in _databaseContext.Apps on config.AppId equals owner.Id
                                        where owner.Id != app.Id && owner.TeamId == app.TeamId
                                        select new { owner.Name, config.DraftDefinition, config.PublishedDefinition })
            .ToListAsync(cancellationToken);
        foreach (var reference in workflowReferences)
        {
            if (ReferencesAgent(reference.DraftDefinition, app.Id) || ReferencesAgent(reference.PublishedDefinition, app.Id))
            {
                throw Referenced(reference.Name);
            }
        }

        await using var transaction = await _databaseContext.Database.BeginTransactionAsync(cancellationToken);
        var channelId = app.Id.ToString();
        await _databaseContext.SoftDeleteAsync(_databaseContext.FeishuAppBindings
            .Where(x => x.ChannelType == (int)FeishuChannelType.App && x.ChannelId == channelId));
        await _databaseContext.SoftDeleteAsync(_databaseContext.DingTalkAppBindings
            .Where(x => x.ChannelType == (int)DingTalkChannelType.App && x.ChannelId == channelId));
        await _databaseContext.SoftDeleteAsync(_databaseContext.WeixinWorkAppBindings
            .Where(x => x.ChannelType == (int)WeixinWorkChannelType.App && x.ChannelId == channelId));
        var deleted = await _databaseContext.SoftDeleteAsync(_databaseContext.Apps.Where(x => x.Id == app.Id));
        if (deleted == 0)
        {
            throw new BusinessException("应用不存在.") { StatusCode = 404 };
        }

        await transaction.CommitAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }

    private static BusinessException Referenced(string name)
        => new($"应用仍被「{name}」的草稿或发布配置引用，请先移除引用后再删除.") { StatusCode = 409 };

    private static bool ReferencesAgent(string? json, Guid appId)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            return nodes.EnumerateArray().Any(node =>
                node.TryGetProperty("type", out var type) && type.GetString() == "agentApp"
                && node.TryGetProperty("config", out var config) && config.ValueKind == JsonValueKind.Object
                && config.TryGetProperty("agentAppId", out var id) && id.ValueKind == JsonValueKind.String
                && Guid.TryParse(id.GetString(), out var target) && target == appId);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
