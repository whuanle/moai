using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="SaveAppAgentConfigCommand"/>
/// </summary>
public class SaveAppAgentConfigCommandHandler : IRequestHandler<SaveAppAgentConfigCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveAppAgentConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public SaveAppAgentConfigCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(SaveAppAgentConfigCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps
            .FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken);

        if (app == null)
        {
            throw new BusinessException("应用不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(app.TeamId, request.ContextUserId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以管理应用.") { StatusCode = 403 };
        }

        if (app.AppType != (int)AppType.Agent)
        {
            throw new BusinessException("只有 Agent 应用支持配置插件、知识库与提示词.") { StatusCode = 400 };
        }

        // 对话模型须在该团队可用；执行参数（execution_settings）尚未开放设置
        var modelId = await ValidateModelIdAsync(app.TeamId, request.ModelId, cancellationToken);
        var wikiIds = await ValidateWikiIdsAsync(app.TeamId, request.WikiIds, cancellationToken);
        var pluginIds = await ValidatePluginIdsAsync(app.TeamId, request.Plugins, cancellationToken);

        var config = await _databaseContext.AppAgentConfigs
            .FirstOrDefaultAsync(x => x.AppId == app.Id, cancellationToken);

        var wikiJson = AppAgentConfigJson.SerializeWikiIds(wikiIds);
        var pluginJson = AppAgentConfigJson.SerializePluginIds(pluginIds);
        var executionJson = NormalizeExecutionSettings(request.ExecutionSettings);

        if (config == null)
        {
            config = new AppAgentConfigEntity
            {
                Id = Guid.CreateVersion7(),
                TeamId = app.TeamId,
                AppId = app.Id,
                Prompt = request.Prompt ?? string.Empty,
                ModelId = modelId,
                WikiIds = wikiJson,
                Plugins = pluginJson,
                ExecutionSettings = executionJson ?? "{}",
            };
            _databaseContext.AppAgentConfigs.Add(config);
        }
        else
        {
            config.Prompt = request.Prompt ?? string.Empty;
            config.ModelId = modelId;
            config.WikiIds = wikiJson;
            config.Plugins = pluginJson;

            // 仅在请求显式携带执行参数时覆盖，避免旧前端保存时清空沙箱等扩展配置
            if (executionJson != null)
            {
                config.ExecutionSettings = executionJson;
            }
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }

    /// <summary>
    /// 校验对话模型：仅允许选择该团队可用的模型（模型与渠道均启用，且为公开模型或已授权给该团队）；
    /// 未选择（null / 空 Guid）时返回空 Guid.
    /// </summary>
    private async Task<Guid> ValidateModelIdAsync(int teamId, Guid? modelId, CancellationToken cancellationToken)
    {
        if (modelId == null || modelId.Value == Guid.Empty)
        {
            return Guid.Empty;
        }

        var row = await _databaseContext.AiModels
            .Where(x => x.Id == modelId.Value && x.Enabled)
            .Join(
                _databaseContext.AiChannels.Where(c => c.Enabled),
                m => m.ChannelId,
                c => c.Id,
                (m, c) => new { m.Id, m.IsPublic })
            .FirstOrDefaultAsync(cancellationToken);

        if (row == null)
        {
            throw new BusinessException("选择的模型不可用，请重新选择.") { StatusCode = 400 };
        }

        if (row.IsPublic)
        {
            return row.Id;
        }

        var authorized = await _databaseContext.AiModelAuthorizations
            .AnyAsync(x => x.TeamId == teamId && x.AiModelId == row.Id, cancellationToken);

        if (!authorized)
        {
            throw new BusinessException("选择的模型该团队无权使用，请重新选择.") { StatusCode = 400 };
        }

        return row.Id;
    }

    /// <summary>
    /// 校验知识库：仅允许绑定本团队的知识库，去重后返回.
    /// </summary>
    private async Task<List<long>> ValidateWikiIdsAsync(int teamId, IReadOnlyCollection<long>? wikiIds, CancellationToken cancellationToken)
    {
        var ids = wikiIds?.Where(x => x > 0).Distinct().ToList() ?? new List<long>();
        if (ids.Count == 0)
        {
            return ids;
        }

        var owned = await _databaseContext.Wikis
            .Where(x => x.TeamId == teamId)
            .Select(x => (long)x.Id)
            .ToListAsync(cancellationToken);

        var ownedSet = owned.ToHashSet();
        var invalid = ids.Where(x => !ownedSet.Contains(x)).ToList();

        if (invalid.Count > 0)
        {
            throw new BusinessException("包含不属于该团队的知识库，请重新选择.") { StatusCode = 400 };
        }

        return ids;
    }

    /// <summary>
    /// 校验插件：仅允许绑定本团队可访问插件（团队自有，或系统插件中已公开/已授权本团队的），去重后返回.
    /// </summary>
    private async Task<List<Guid>> ValidatePluginIdsAsync(int teamId, IReadOnlyCollection<Guid>? pluginIds, CancellationToken cancellationToken)
    {
        var ids = pluginIds?.Where(x => x != Guid.Empty).Distinct().ToList() ?? new List<Guid>();
        if (ids.Count == 0)
        {
            return ids;
        }

        var rows = await _databaseContext.Plugins
            .Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.TeamId, x.IsSystem, x.IsPublic })
            .ToListAsync(cancellationToken);

        var authorized = await _databaseContext.PluginTeamAuthorizations
            .Where(x => x.TeamId == teamId)
            .Select(x => x.PluginId)
            .ToListAsync(cancellationToken);

        var authorizedSet = authorized.ToHashSet();
        var rowMap = rows.ToDictionary(x => x.Id);
        var invalid = new List<Guid>();

        foreach (var id in ids)
        {
            if (!rowMap.TryGetValue(id, out var row))
            {
                invalid.Add(id);
                continue;
            }

            // 团队自有插件
            if (row.TeamId == teamId)
            {
                continue;
            }

            // 系统插件：公开或已授权本团队
            var accessible = row.IsSystem && row.TeamId == 0 && (row.IsPublic || authorizedSet.Contains(id));
            if (!accessible)
            {
                invalid.Add(id);
            }
        }

        if (invalid.Count > 0)
        {
            throw new BusinessException("包含该团队无权使用的插件，请重新选择.") { StatusCode = 400 };
        }

        return ids;
    }

    /// <summary>
    /// 规范化执行参数 JSON：null / Null 值返回 null（表示不覆盖），对象返回其原始文本.
    /// </summary>
    private static string? NormalizeExecutionSettings(JsonElement? executionSettings)
    {
        if (executionSettings is null || executionSettings.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (executionSettings.Value.ValueKind != JsonValueKind.Object)
        {
            throw new BusinessException("执行参数必须是 JSON 对象.") { StatusCode = 400 };
        }

        return executionSettings.Value.GetRawText();
    }
}
