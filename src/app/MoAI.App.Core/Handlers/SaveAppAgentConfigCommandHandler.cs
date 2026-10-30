using System.Text.Json;
using System.Text.Json.Nodes;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.App.Services;
using MoAI.App.Validation;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Database.Seed;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Models;
using MoAI.Settings.Services;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="SaveAppAgentConfigCommand"/>
/// </summary>
public class SaveAppAgentConfigCommandHandler : IRequestHandler<SaveAppAgentConfigCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly ISandboxSettingsService _sandboxSettingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveAppAgentConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="sandboxSettingsService">沙箱上限设置读取服务.</param>
    public SaveAppAgentConfigCommandHandler(DatabaseContext databaseContext, ITeamService teamService, ISandboxSettingsService sandboxSettingsService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _sandboxSettingsService = sandboxSettingsService;
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

        if (app.AppType != (int)AppType.Agent && app.AppType != (int)AppType.Workflow)
        {
            throw new BusinessException("只有 Agent 应用支持配置插件、知识库与提示词.") { StatusCode = 400 };
        }

        // 流程应用无模型/知识库/插件/技能编排，仅经此入口维护对话开场白；其余字段不适用、不校验不落库
        if (app.AppType == (int)AppType.Workflow)
        {
            await UpsertOpeningStatementAsync(app, request, cancellationToken);
            return EmptyCommandResponse.Default;
        }

        // 外部应用面向外部用户/匿名开放，不允许技能与沙箱：显式携带即拒绝；
        // 历史存量配置由对话装配兜底强制关闭，此处保存时同时收敛落库
        if (app.IsExternal)
        {
            if (request.Skills is { Count: > 0 })
            {
                throw new BusinessException("外部应用不能绑定技能，请移除技能配置.") { StatusCode = 400 };
            }

            if (SandboxSettingsLimitValidator.IsSandboxEnabled(request.ExecutionSettings))
            {
                throw new BusinessException("外部应用不能开启沙箱.") { StatusCode = 400 };
            }

            if (SubAgentSettingsValidator.IsSubAgentEnabled(request.ExecutionSettings))
            {
                throw new BusinessException("外部应用不能开启子代理协作.") { StatusCode = 400 };
            }

            if (MemorySettingsValidator.IsMemoryEnabled(request.ExecutionSettings))
            {
                throw new BusinessException("外部应用不能开启长期记忆.") { StatusCode = 400 };
            }
        }

        // 对话模型须在该团队可用；执行参数含沙箱等扩展配置，启用沙箱时受系统上限约束
        var modelId = await ValidateModelIdAsync(app.TeamId, request.ModelId, cancellationToken);
        var wikiIds = await ValidateWikiIdsAsync(app.TeamId, request.WikiIds, cancellationToken);
        var graphIds = await ValidateGraphIdsAsync(app.TeamId, request.GraphIds, cancellationToken);
        var pluginIds = await ValidatePluginIdsAsync(app.TeamId, request.Plugins, cancellationToken);
        var skillIds = await ValidateSkillIdsAsync(app.TeamId, request.Skills, cancellationToken);
        var workflowAppIds = await ValidateWorkflowAppIdsAsync(app.TeamId, request.WorkflowApps, cancellationToken);
        var agentAppIds = await ValidateAgentAppIdsAsync(app, request.AgentApps, cancellationToken);

        // 子代理协作（execution_settings.subAgent）：取值范围限幅校验
        if (request.ExecutionSettings != null)
        {
            SubAgentSettingsValidator.Validate(request.ExecutionSettings.Value);
            MemorySettingsValidator.Validate(request.ExecutionSettings.Value);
        }


        var config = await _databaseContext.AppAgentConfigs
            .FirstOrDefaultAsync(x => x.AppId == app.Id, cancellationToken);

        // 循环嵌套防护：本次保存携带 Agent 应用绑定时，以保存后的草稿全量（请求值 ?? 库中现值）
        // 仅沿 Agent→Agent 工具边做闭包检测（流程工具相关的环由流程侧守卫负责，Agent 侧保存不拒绝）
        if (request.AgentApps != null)
        {
            var effectiveAgentTools = agentAppIds ?? AppAgentConfigJson.ParseGuidList(config?.AgentApps);
            await AppBindingCycleGuard.EnsureNoCycleFromAgentAsync(_databaseContext, app.Id, effectiveAgentTools, cancellationToken);
        }

        // 审批策略（execution_settings.toolApproval）：六类自动放行白名单必须是生效绑定列表的子集
        // （流程/Agent 应用与技能未携带时保持原值，按库中现值校验；外部应用技能固定为空）
        ValidateToolApprovalPolicy(
            request.ExecutionSettings,
            pluginIds,
            workflowAppIds ?? AppAgentConfigJson.ParseGuidList(config?.WorkflowApps),
            agentAppIds ?? AppAgentConfigJson.ParseGuidList(config?.AgentApps),
            app.IsExternal ? [] : skillIds ?? AppAgentConfigJson.ParseGuidList(config?.Skills),
            wikiIds,
            graphIds);

        var wikiJson = AppAgentConfigJson.SerializeWikiIds(wikiIds);
        var graphJson = AppAgentConfigJson.SerializeGraphIds(graphIds);
        var pluginJson = AppAgentConfigJson.SerializePluginIds(pluginIds);
        var executionJson = NormalizeExecutionSettings(request.ExecutionSettings);
        // 快捷输入：仅请求显式携带时覆盖（去空白、去重），避免旧前端保存其他字段时清空
        var quickInputs = NormalizeQuickInputs(request.QuickInputs);

        // 本次保存携带执行参数且启用沙箱时，存活时间 / CPU / 内存不得超出系统设置的上限
        if (executionJson != null)
        {
            var sandboxLimits = await _sandboxSettingsService.GetLimitsAsync(cancellationToken);
            SandboxSettingsLimitValidator.Validate(request.ExecutionSettings!.Value, sandboxLimits);
        }

        // 长期记忆（execution_settings.memory）：向量化模型/维度统一由系统设置决定，开启时校验平台总开关
        if (executionJson != null)
        {
            executionJson = await ValidateAndNormalizeMemoryAsync(executionJson, cancellationToken);
        }

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
                GraphIds = graphJson,
                Plugins = pluginJson,
                WorkflowApps = AppAgentConfigJson.SerializePluginIds(workflowAppIds ?? []),
                AgentApps = AppAgentConfigJson.SerializePluginIds(agentAppIds ?? []),
                Skills = AppAgentConfigJson.SerializePluginIds(skillIds ?? []),
                ExecutionSettings = executionJson ?? "{}",
                OpeningStatement = request.OpeningStatement ?? string.Empty,
                OpeningStatementEnabled = request.OpeningStatementEnabled,
                QuickInputs = AppAgentConfigJson.SerializeStrings(quickInputs ?? []),
            };
            _databaseContext.AppAgentConfigs.Add(config);
        }
        else
        {
            config.Prompt = request.Prompt ?? string.Empty;
            config.ModelId = modelId;
            config.WikiIds = wikiJson;
            config.GraphIds = graphJson;
            config.Plugins = pluginJson;
            config.OpeningStatement = request.OpeningStatement ?? string.Empty;
            config.OpeningStatementEnabled = request.OpeningStatementEnabled;

            if (quickInputs != null)
            {
                config.QuickInputs = AppAgentConfigJson.SerializeStrings(quickInputs);
            }

            // 仅在请求显式携带流程应用列表时覆盖，避免旧前端保存时清空流程应用绑定
            if (workflowAppIds != null)
            {
                config.WorkflowApps = AppAgentConfigJson.SerializePluginIds(workflowAppIds);
            }

            // 仅在请求显式携带 Agent 应用列表时覆盖，避免旧前端保存时清空 Agent 应用绑定
            if (agentAppIds != null)
            {
                config.AgentApps = AppAgentConfigJson.SerializePluginIds(agentAppIds);
            }

            // 仅在请求显式携带技能列表时覆盖，避免旧前端保存时清空技能配置
            if (skillIds != null)
            {
                config.Skills = AppAgentConfigJson.SerializePluginIds(skillIds);
            }

            // 外部应用不允许技能：请求未携带时也强制清空，历史存量配置随保存收敛
            if (app.IsExternal)
            {
                config.Skills = "[]";
            }

            // 仅在请求显式携带执行参数时覆盖，避免旧前端保存时清空沙箱等扩展配置
            if (executionJson != null)
            {
                config.ExecutionSettings = executionJson;
            }

            // 保存即草稿变更：与已发布快照不一致，正式会话仍按快照执行
            config.Status = 0;
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }

    /// <summary>
    /// 流程应用只保存对话开场白（配置行不存在时创建，其余字段留默认值）.
    /// </summary>
    private async Task UpsertOpeningStatementAsync(AppEntity app, SaveAppAgentConfigCommand request, CancellationToken cancellationToken)
    {
        var config = await _databaseContext.AppAgentConfigs
            .FirstOrDefaultAsync(x => x.AppId == app.Id, cancellationToken);

        if (config == null)
        {
            config = new AppAgentConfigEntity
            {
                Id = Guid.CreateVersion7(),
                TeamId = app.TeamId,
                AppId = app.Id,
                OpeningStatement = request.OpeningStatement ?? string.Empty,
                OpeningStatementEnabled = request.OpeningStatementEnabled,
            };
            _databaseContext.AppAgentConfigs.Add(config);
        }
        else
        {
            config.OpeningStatement = request.OpeningStatement ?? string.Empty;
            config.OpeningStatementEnabled = request.OpeningStatementEnabled;

            // 开场白草稿变更：与已发布快照不一致，重新发布后生效
            config.Status = 0;
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);
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

        // 与 AiModelResolver 同口径：团队自有渠道 / 公开模型 / 授权模型均可用.
        var row = await _databaseContext.AiModels
            .Where(x => x.Id == modelId.Value && x.Enabled)
            .Join(
                _databaseContext.AiChannels.Where(c => c.Enabled),
                m => m.ChannelId,
                c => c.Id,
                (m, c) => new { m.Id, m.IsPublic, c.TeamId })
            .FirstOrDefaultAsync(cancellationToken);

        if (row == null)
        {
            throw new BusinessException("选择的模型不可用，请重新选择.") { StatusCode = 400 };
        }

        if (row.IsPublic || row.TeamId == teamId)
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
    /// 校验知识图谱：仅允许绑定本团队的平台托管图（接入图不支持应用侧检索绑定），去重后返回.
    /// </summary>
    private async Task<List<long>> ValidateGraphIdsAsync(int teamId, IReadOnlyCollection<long>? graphIds, CancellationToken cancellationToken)
    {
        var ids = graphIds?.Where(x => x > 0).Distinct().ToList() ?? new List<long>();
        if (ids.Count == 0)
        {
            return ids;
        }

        var owned = await _databaseContext.KnowledgeGraphs
            .Where(x => x.TeamId == teamId && x.Mode == KnowledgeGraphModes.Managed)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var ownedSet = owned.ToHashSet();
        var invalid = ids.Where(x => !ownedSet.Contains(x)).ToList();

        if (invalid.Count > 0)
        {
            throw new BusinessException("包含不属于该团队或不支持检索的知识图谱，请重新选择.") { StatusCode = 400 };
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
    /// 校验流程应用绑定：仅允许绑定本团队已发布、未禁用的内部流程应用（执行时按其发布快照驱动），去重后返回；
    /// null 表示请求未携带该字段（保持原值）.
    /// </summary>
    private async Task<List<Guid>?> ValidateWorkflowAppIdsAsync(int teamId, IReadOnlyCollection<Guid>? workflowAppIds, CancellationToken cancellationToken)
    {
        if (workflowAppIds == null)
        {
            return null;
        }

        var ids = workflowAppIds.Where(x => x != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
        {
            return ids;
        }

        var validCount = await _databaseContext.Apps
            .CountAsync(x => ids.Contains(x.Id)
                && x.TeamId == teamId
                && !x.IsExternal
                && !x.IsDisable
                && x.AppType == (int)AppType.Workflow
                && x.PublishStatus == 1, cancellationToken);

        if (validCount != ids.Count)
        {
            throw new BusinessException("包含不存在、未发布或无权使用的流程应用，请重新选择.") { StatusCode = 400 };
        }

        return ids;
    }

    /// <summary>
    /// 校验 Agent 应用绑定：仅允许绑定本团队已发布、未禁用的内部 Agent 应用且不含自身（执行时按其发布快照驱动），
    /// 去重后返回；null 表示请求未携带该字段（保持原值）.
    /// </summary>
    private async Task<List<Guid>?> ValidateAgentAppIdsAsync(AppEntity app, IReadOnlyCollection<Guid>? agentAppIds, CancellationToken cancellationToken)
    {
        if (agentAppIds == null)
        {
            return null;
        }

        var ids = agentAppIds.Where(x => x != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
        {
            return ids;
        }

        if (ids.Contains(app.Id))
        {
            throw new BusinessException("Agent 应用不能绑定自身作为工具.") { StatusCode = 400 };
        }

        var validCount = await _databaseContext.Apps
            .CountAsync(x => ids.Contains(x.Id)
                && x.TeamId == app.TeamId
                && !x.IsExternal
                && !x.IsDisable
                && x.AppType == (int)AppType.Agent
                && x.PublishStatus == 1, cancellationToken);

        if (validCount != ids.Count)
        {
            throw new BusinessException("包含不存在、未发布或无权使用的 Agent 应用，请重新选择.") { StatusCode = 400 };
        }

        return ids;
    }

    /// <summary>
    /// 校验技能：仅允许配置启用中的系统内置/市场公开/本团队技能（用户自选范围与管理端一致），去重后返回；
    /// null 表示请求未携带技能字段（保持原值）.
    /// </summary>
    private async Task<List<Guid>?> ValidateSkillIdsAsync(int teamId, IReadOnlyCollection<Guid>? skillIds, CancellationToken cancellationToken)
    {
        if (skillIds == null)
        {
            return null;
        }

        var ids = skillIds.Where(x => x != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
        {
            return ids;
        }

        var validCount = await _databaseContext.Skills
            .CountAsync(x => ids.Contains(x.Id) && !x.IsDisable
                && (x.IsSystem || x.IsPublic || x.TeamId == teamId), cancellationToken);

        if (validCount != ids.Count)
        {
            throw new BusinessException("包含不存在、已禁用或无权使用的技能，请重新选择.") { StatusCode = 400 };
        }

        return ids;
    }

    /// <summary>
    /// 校验并规范化长期记忆配置（execution_settings.memory）：向量化模型/维度统一由系统设置（MEMORY_*）决定，
    /// 应用配置不接收 embeddingModelId/embeddingDimensions（携带时剥离，历史存量值随保存收敛）；
    /// 开启记忆时要求平台已开启长期记忆且已选统一向量化模型，否则拒绝.
    /// </summary>
    /// <param name="executionJson">本次保存的执行参数 JSON 文本.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>规范化后的执行参数 JSON 文本.</returns>
    private async Task<string> ValidateAndNormalizeMemoryAsync(string executionJson, CancellationToken cancellationToken)
    {
        var root = JsonNode.Parse(executionJson) as JsonObject;
        if (root == null)
        {
            return executionJson;
        }

        if (!root.TryGetPropertyValue("memory", out var memoryNode) || memoryNode is not JsonObject memory)
        {
            return executionJson;
        }

        // 模型/维度统一在系统设置：应用配置一律不落库
        memory.Remove("embeddingModelId");
        memory.Remove("embeddingDimensions");

        var requestEnabled = memory.TryGetPropertyValue("enabled", out var enabledNode)
            && enabledNode is JsonValue enabledValue && enabledValue.TryGetValue<bool>(out var enabledFlag) && enabledFlag;
        if (requestEnabled && !await IsPlatformMemoryUsableAsync(cancellationToken))
        {
            throw new BusinessException("平台未开启长期记忆，请先在系统设置中开启.") { StatusCode = 400 };
        }

        return root.ToJsonString();
    }

    /// <summary>
    /// 解析平台长期记忆是否可用（MEMORY_ENABLED=true 且已选统一向量化模型）.
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

    /// <summary>
    /// 校验审批策略（execution_settings.toolApproval）：sandboxAutoApproved 须为布尔值，
    /// 六类自动放行白名单（插件/流程应用/Agent 应用/技能为字符串 id 数组，知识库/知识图谱为数字 id 数组）
    /// 须全部在生效绑定列表范围内（请求未携带的绑定字段按库中现值）；未携带 toolApproval 节时跳过.
    /// </summary>
    private static void ValidateToolApprovalPolicy(
        JsonElement? executionSettings,
        IReadOnlyList<Guid> boundPluginIds,
        IReadOnlyList<Guid> boundWorkflowAppIds,
        IReadOnlyList<Guid> boundAgentAppIds,
        IReadOnlyList<Guid> boundSkillIds,
        IReadOnlyList<long> boundWikiIds,
        IReadOnlyList<long> boundGraphIds)
    {
        if (executionSettings is null || executionSettings.Value.ValueKind is not JsonValueKind.Object)
        {
            return;
        }

        if (!executionSettings.Value.TryGetProperty("toolApproval", out var node)
            || node.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return;
        }

        if (node.ValueKind != JsonValueKind.Object)
        {
            throw new BusinessException("审批策略必须是 JSON 对象.") { StatusCode = 400 };
        }

        if (node.TryGetProperty("sandboxAutoApproved", out var sandbox)
            && sandbox.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new BusinessException("审批策略的沙箱自动放行开关必须为布尔值.") { StatusCode = 400 };
        }

        ValidateGuidWhitelist(node, "autoApprovePlugins", boundPluginIds, "审批策略的自动放行插件必须是插件 id 数组.", "自动放行插件必须为当前已绑定的插件，请先在插件列表中绑定.");
        ValidateGuidWhitelist(node, "autoApproveWorkflowApps", boundWorkflowAppIds, "审批策略的自动放行流程应用必须是应用 id 数组.", "自动放行流程应用必须为当前已绑定的流程应用，请先绑定流程应用.");
        ValidateGuidWhitelist(node, "autoApproveAgentApps", boundAgentAppIds, "审批策略的自动放行 Agent 应用必须是应用 id 数组.", "自动放行 Agent 应用必须为当前已绑定的 Agent 应用，请先绑定 Agent 应用.");
        ValidateGuidWhitelist(node, "autoApproveSkills", boundSkillIds, "审批策略的自动放行技能必须是技能 id 数组.", "自动放行技能必须为当前已绑定的技能，请先绑定技能.");
        ValidateLongWhitelist(node, "autoApproveWikis", boundWikiIds, "审批策略的自动放行知识库必须是知识库 id 数组.", "自动放行知识库必须为当前已绑定的知识库，请先绑定知识库.");
        ValidateLongWhitelist(node, "autoApproveGraphs", boundGraphIds, "审批策略的自动放行知识图谱必须是知识图谱 id 数组.", "自动放行知识图谱必须为当前已绑定的知识图谱，请先绑定知识图谱.");
    }

    /// <summary>
    /// 校验 Guid 白名单数组：须为字符串数组且全部在绑定列表内；未携带该键时跳过.
    /// </summary>
    private static void ValidateGuidWhitelist(JsonElement node, string property, IReadOnlyList<Guid> boundIds, string shapeErrorMessage, string rangeErrorMessage)
    {
        if (!node.TryGetProperty(property, out var list) || list.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return;
        }

        if (list.ValueKind != JsonValueKind.Array)
        {
            throw new BusinessException(shapeErrorMessage) { StatusCode = 400 };
        }

        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String
                || !Guid.TryParse(item.GetString(), out var id)
                || id == Guid.Empty
                || !boundIds.Contains(id))
            {
                throw new BusinessException(rangeErrorMessage) { StatusCode = 400 };
            }
        }
    }

    /// <summary>
    /// 校验 long 白名单数组：须为数字数组且全部在绑定列表内；未携带该键时跳过.
    /// </summary>
    private static void ValidateLongWhitelist(JsonElement node, string property, IReadOnlyList<long> boundIds, string shapeErrorMessage, string rangeErrorMessage)
    {
        if (!node.TryGetProperty(property, out var list) || list.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return;
        }

        if (list.ValueKind != JsonValueKind.Array)
        {
            throw new BusinessException(shapeErrorMessage) { StatusCode = 400 };
        }

        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number
                || !item.TryGetInt64(out var id)
                || id <= 0
                || !boundIds.Contains(id))
            {
                throw new BusinessException(rangeErrorMessage) { StatusCode = 400 };
            }
        }
    }

    /// <summary>
    /// 规范化快捷输入：去首尾空白、丢弃空串、去重；null 表示请求未携带该字段（保持原值）.
    /// </summary>
    private static List<string>? NormalizeQuickInputs(IReadOnlyCollection<string>? quickInputs)
    {
        if (quickInputs == null)
        {
            return null;
        }

        return quickInputs
            .Select(x => x?.Trim() ?? string.Empty)
            .Where(x => x.Length > 0)
            .Distinct()
            .ToList();
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
