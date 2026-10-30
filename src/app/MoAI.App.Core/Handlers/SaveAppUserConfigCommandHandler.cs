using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.App.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Seed;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="SaveAppUserConfigCommand"/>
/// </summary>
public class SaveAppUserConfigCommandHandler : IRequestHandler<SaveAppUserConfigCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveAppUserConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public SaveAppUserConfigCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(SaveAppUserConfigCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps.FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken)
            ?? throw new BusinessException("应用不存在.") { StatusCode = 404 };

        if (app.IsDisable)
        {
            throw new BusinessException("应用已被禁用.") { StatusCode = 403 };
        }

        var myRole = await _teamService.GetMyRoleAsync(app.TeamId, request.ContextUserId, cancellationToken);
        var canUseAsPublic = app.IsPublic && app.PublishStatus == 1;
        if (myRole == null && !canUseAsPublic)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (request.PromptId != 0)
        {
            await SessionPromptHelper.EnsureUsableAsync(_databaseContext, _teamService, request.PromptId, app.TeamId, request.ContextUserId, cancellationToken);
        }

        // 应用默认技能集合：用户勾选只能落在该集合内
        var skillsJson = await _databaseContext.AppAgentConfigs.AsNoTracking()
            .Where(x => x.AppId == request.AppId)
            .Select(x => x.Skills)
            .FirstOrDefaultAsync(cancellationToken);
        var defaultSkillIds = AppAgentConfigJson.ParseGuidList(skillsJson).ToHashSet();

        var skills = request.Skills?.Distinct().ToList() ?? new List<Guid>();
        if (skills.Count > 0 && skills.Any(x => !defaultSkillIds.Contains(x)))
        {
            throw new BusinessException("存在应用未开放的技能，请在应用设置中重新选择.") { StatusCode = 400 };
        }

        // 长期记忆模式：应用开启记忆时才允许用户调整；跨应用选项须应用授权（ScopeDefault=crossApp），关闭记忆时仅允许 0/1
        // 长期记忆偏好（模式与全局接入）：应用开启记忆时才允许用户调整；未开启时拒绝
        if (request.MemoryMode.HasValue || request.MemoryGlobalMode.HasValue)
        {
            var configRow = await _databaseContext.AppAgentConfigs.AsNoTracking()
                .Where(x => x.AppId == request.AppId)
                .Select(x => new { x.ExecutionSettings, x.PublishedConfig })
                .FirstOrDefaultAsync(cancellationToken);
            var effectiveSettings = app.PublishStatus == 1
                ? MoAI.Database.Aggregates.AppAgentConfigSnapshot.TryParse(configRow?.PublishedConfig)?.ExecutionSettings ?? configRow?.ExecutionSettings
                : configRow?.ExecutionSettings;
            var memorySettings = MoAI.AI.Models.AgentMemorySettings.ParseExecutionSettings(effectiveSettings);
            // 平台未开启长期记忆（MEMORY_* 总开关/模型未就绪）时与应用未开启同责：运行时一律不生效
            var memoryEnabled = memorySettings is { Enabled: true } && await IsPlatformMemoryUsableAsync(cancellationToken);
            // 应用开启记忆时用户可自由选择模式；未开启仅允许记忆模式 0/1，全局接入直接拒绝
            var mode = request.MemoryMode ?? MoAI.AI.Models.AgentMemorySettings.UserModeFollowApp;
            if ((!memoryEnabled && mode != MoAI.AI.Models.AgentMemorySettings.UserModeFollowApp && mode != MoAI.AI.Models.AgentMemorySettings.UserModeOff)
                || (!memoryEnabled && request.MemoryGlobalMode.HasValue))
            {
                throw new BusinessException("该应用未开启长期记忆.") { StatusCode = 400 };
            }
        }

        var userConfig = await _databaseContext.AppUserConfigs
            .FirstOrDefaultAsync(x => x.AppId == request.AppId && x.UserId == request.ContextUserId, cancellationToken);

        if (userConfig == null)
        {
            userConfig = new AppUserConfigEntity
            {
                Id = Guid.CreateVersion7(),
                AppId = request.AppId,
                UserId = request.ContextUserId,
                TeamId = app.TeamId,
                PromptId = request.PromptId,
                Skills = JsonSerializer.Serialize(skills.Select(x => x.ToString()).ToList()),
                ToolApprovalMode = MoAI.AI.AppToolApprovalContract.IsValidMode(request.ToolApprovalMode)
                    ? request.ToolApprovalMode!
                    : MoAI.AI.AppToolApprovalContract.ModeAuto,
                MemoryMode = (short)(request.MemoryMode ?? MoAI.AI.Models.AgentMemorySettings.UserModeFollowApp),
                MemoryGlobalMode = (short)(request.MemoryGlobalMode ?? MoAI.AI.Models.AgentMemorySettings.UserGlobalModeOn),
            };
            _databaseContext.AppUserConfigs.Add(userConfig);
        }
        else
        {
            userConfig.PromptId = request.PromptId;
            userConfig.Skills = JsonSerializer.Serialize(skills.Select(x => x.ToString()).ToList());
            if (MoAI.AI.AppToolApprovalContract.IsValidMode(request.ToolApprovalMode))
            {
                userConfig.ToolApprovalMode = request.ToolApprovalMode!;
            }

            if (request.MemoryMode.HasValue)
            {
                userConfig.MemoryMode = (short)request.MemoryMode.Value;
            }

            if (request.MemoryGlobalMode.HasValue)
            {
                userConfig.MemoryGlobalMode = (short)request.MemoryGlobalMode.Value;
            }
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }

    /// <summary>
    /// 解析平台长期记忆是否可用（MEMORY_ENABLED=true 且已选统一向量化模型），与应用配置共同决定用户记忆偏好是否可调整.
    /// </summary>
    private async Task<bool> IsPlatformMemoryUsableAsync(CancellationToken cancellationToken)
    {
        var keys = new[]
        {
            SettingDefinitions.MemoryEnabledKey,
            SettingDefinitions.MemoryEmbeddingModelKey,
        };
        var values = await _databaseContext.Settings.AsNoTracking()
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);
        string Resolve(string key) => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : SettingDefinitions.Find(key)?.DefaultValue ?? string.Empty;
        var enabled = string.Equals(Resolve(SettingDefinitions.MemoryEnabledKey).Trim(), "true", StringComparison.OrdinalIgnoreCase);
        var hasModel = Guid.TryParse(Resolve(SettingDefinitions.MemoryEmbeddingModelKey).Trim(), out var modelId) && modelId != Guid.Empty;
        return enabled && hasModel;
    }
}
