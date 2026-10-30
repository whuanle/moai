using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIPlugin.Models;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.Database;
using MoAI.Database.Aggregates;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="QueryAppUserConfigCommand"/>
/// </summary>
public class QueryAppUserConfigCommandHandler : IRequestHandler<QueryAppUserConfigCommand, QueryAppUserConfigCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAppUserConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public QueryAppUserConfigCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<QueryAppUserConfigCommandResponse> Handle(QueryAppUserConfigCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps.FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken)
            ?? throw new BusinessException("应用不存在.") { StatusCode = 404 };

        var myRole = await _teamService.GetMyRoleAsync(app.TeamId, request.ContextUserId, cancellationToken);
        var canUseAsPublic = app.IsPublic && app.PublishStatus == 1;
        if (myRole == null && !canUseAsPublic)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        var config = await _databaseContext.AppAgentConfigs.AsNoTracking()
            .Where(x => x.AppId == request.AppId)
            .Select(x => new AppApprovalConfigRow(x.Skills, x.ExecutionSettings, x.PublishedConfig, x.WikiIds, x.GraphIds, x.WorkflowApps, x.AgentApps))
            .FirstOrDefaultAsync(cancellationToken);

        var defaultSkillIds = ParseGuidList(config?.Skills);

        // 应用默认技能目录（按配置顺序）；技能被删除/禁用时目录静默缺失，用户勾选随之失效
        var defaultSkills = new List<AppUserSkillOption>();
        if (defaultSkillIds.Count > 0)
        {
            var rows = await _databaseContext.Skills.AsNoTracking()
                .Where(x => defaultSkillIds.Contains(x.Id) && !x.IsDisable)
                .Select(x => new { x.Id, x.Key, x.Name, x.Description, x.IsSystem, x.TeamId, x.UseCount })
                .ToDictionaryAsync(x => x.Id, cancellationToken);
            defaultSkills = defaultSkillIds
                .Where(rows.ContainsKey)
                .Select(id => new AppUserSkillOption
                {
                    Id = rows[id].Id,
                    Key = rows[id].Key,
                    Name = rows[id].Name,
                    Description = rows[id].Description,
                    IsSystem = rows[id].IsSystem,
                    TeamId = rows[id].TeamId,
                    UseCount = rows[id].UseCount,
                })
                .ToList();
        }

        var availableSkillIds = defaultSkills.Select(x => x.Id).ToHashSet();

        var userConfig = await _databaseContext.AppUserConfigs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.AppId == request.AppId && x.UserId == request.ContextUserId, cancellationToken);

        // 无用户配置行时默认技能全部启用；有配置行时勾选为默认技能与用户选择的交集（按默认技能顺序）
        IReadOnlyList<Guid> selectedSkills;
        if (userConfig == null)
        {
            selectedSkills = defaultSkillIds.Where(availableSkillIds.Contains).ToList();
        }
        else
        {
            var userSkillSet = ParseGuidList(userConfig.Skills).ToHashSet();
            selectedSkills = defaultSkillIds.Where(id => availableSkillIds.Contains(id) && userSkillSet.Contains(id)).ToList();
        }

        // 审批策略按发布快照解析（与对话闸口一致）：白名单资源与沙箱在审批模式下自动放行，前端据此免展示审批卡
        var (autoNames, autoPrefixes) = await ResolveAutoApprovedToolsAsync(app, config, cancellationToken);

        // 记忆偏好：按发布快照（已发布应用）解析应用记忆配置，返回用户存储模式与应用开启状态
        int memoryMode = userConfig?.MemoryMode ?? 0;
        int memoryGlobalMode = userConfig?.MemoryGlobalMode ?? 0;
        var memoryEnabled = false;
        var memoryGlobalAllowed = false;
        var memoryConfigRow = await _databaseContext.AppAgentConfigs.AsNoTracking()
            .Where(x => x.AppId == request.AppId)
            .Select(x => new { x.ExecutionSettings, x.PublishedConfig })
            .FirstOrDefaultAsync(cancellationToken);
        var memoryEffectiveSettings = app.PublishStatus == 1
            ? AppAgentConfigSnapshot.TryParse(memoryConfigRow?.PublishedConfig)?.ExecutionSettings ?? memoryConfigRow?.ExecutionSettings
            : memoryConfigRow?.ExecutionSettings;
        var memorySettings = MoAI.AI.Models.AgentMemorySettings.ParseExecutionSettings(memoryEffectiveSettings);
        memoryEnabled = memorySettings is { Enabled: true };
        memoryGlobalAllowed = memorySettings is { Enabled: true, AllowGlobalMemory: true };

        return new QueryAppUserConfigCommandResponse
        {
            PromptId = userConfig?.PromptId ?? 0,
            MemoryMode = memoryMode,
            MemoryEnabled = memoryEnabled,
            MemoryGlobalMode = memoryGlobalMode,
            MemoryGlobalAllowed = memoryGlobalAllowed,
            Skills = selectedSkills,
            DefaultSkills = defaultSkills,
            ToolApprovalMode = string.IsNullOrWhiteSpace(userConfig?.ToolApprovalMode)
                ? MoAI.AI.AppToolApprovalContract.ModeAuto
                : userConfig!.ToolApprovalMode,
            ToolApprovalExemptNames = MoAI.AI.AppToolApprovalContract.ExemptToolNames,
            ToolApprovalExemptPrefixes = MoAI.AI.AppToolApprovalContract.ExemptToolPrefixes,
            ToolApprovalAutoApprovedNames = autoNames,
            ToolApprovalAutoApprovedPrefixes = autoPrefixes,
        };
    }

    /// <summary>
    /// 审批策略解析所需的配置行投影（草稿）：绑定列表 JSON 与执行参数/发布快照.
    /// </summary>
    private sealed record AppApprovalConfigRow(
        string? Skills,
        string? ExecutionSettings,
        string? PublishedConfig,
        string? WikiIds,
        string? GraphIds,
        string? WorkflowApps,
        string? AgentApps);

    /// <summary>
    /// 解析审批策略自动放行的工具名/前缀：已发布应用按发布快照的执行参数（与闸口一致），未发布按实时草稿；
    /// 六类白名单解析为工具名——插件（原生=插件名，MCP/OpenAPI=插件名__函数名）、流程应用（workflow__{应用名}，
    /// 同名加短 id 后缀，与闸口装配同一去重规则）、Agent 应用（agent__{应用名}，同上）、技能（skill_{key}）；
    /// 知识库/知识图谱检索工具覆盖全部绑定资源，须全部绑定资源均命中白名单才下发工具名；沙箱开关解析为 sandbox_ 前缀.
    /// </summary>
    private async Task<(List<string> Names, List<string> Prefixes)> ResolveAutoApprovedToolsAsync(
        AppEntity app,
        AppApprovalConfigRow? config,
        CancellationToken cancellationToken)
    {
        var snapshot = app.PublishStatus == 1 ? AppAgentConfigSnapshot.TryParse(config?.PublishedConfig) : null;
        var policy = MoAI.AI.AppToolApprovalPolicy.Parse(snapshot?.ExecutionSettings ?? config?.ExecutionSettings);

        var prefixes = new List<string>();
        if (policy.SandboxAutoApproved)
        {
            prefixes.Add(MoAI.AI.AppToolApprovalContract.SandboxToolPrefix);
        }

        var names = new List<string>();

        // 插件：原生（静态/动态）一个插件一个工具，工具名即插件名；自定义（MCP/OpenAPI）按函数展开为 {插件名}__{函数名}
        if (policy.AutoApprovePlugins.Count > 0)
        {
            var ids = policy.AutoApprovePlugins;
            var plugins = await _databaseContext.Plugins.AsNoTracking()
                .Where(x => ids.Contains(x.Id))
                .Select(x => new { x.PluginId, x.PluginName, x.Type })
                .ToListAsync(cancellationToken);

            names.AddRange(plugins
                .Where(x => x.Type == (int)PluginType.NativePlugin)
                .Select(x => x.PluginName));

            var customIds = plugins
                .Where(x => x.Type == (int)PluginType.MCP || x.Type == (int)PluginType.OpenApi)
                .ToList();
            if (customIds.Count > 0)
            {
                var customKeys = customIds.Select(x => x.PluginId).ToList();
                var functions = await _databaseContext.PluginFunctions.AsNoTracking()
                    .Where(x => customKeys.Contains(x.PluginCustomId))
                    .Select(x => new { x.PluginCustomId, x.Name })
                    .ToListAsync(cancellationToken);
                var nameByCustomId = customIds.ToDictionary(x => x.PluginId, x => x.PluginName);
                names.AddRange(functions
                    .Where(x => nameByCustomId.ContainsKey(x.PluginCustomId))
                    .Select(x => MoAI.AI.AppPluginToolNaming.FunctionToolName(nameByCustomId[x.PluginCustomId], x.Name)));
            }
        }

        // 流程应用与 Agent 应用：按生效绑定的全量应用复现闸口装配的同名去重命名，再保留白名单应用的工具名
        await AddApprovedAppToolNamesAsync(names, AppAgentConfigJson.ParseGuidList(snapshot?.WorkflowApps ?? config?.WorkflowApps), policy.AutoApproveWorkflowApps, MoAI.AI.AppToolApprovalContract.WorkflowToolPrefix, cancellationToken);
        await AddApprovedAppToolNamesAsync(names, AppAgentConfigJson.ParseGuidList(snapshot?.AgentApps ?? config?.AgentApps), policy.AutoApproveAgentApps, MoAI.AI.AppToolApprovalContract.AgentAppToolPrefix, cancellationToken);

        // 技能：装载工具名 skill_{key}
        if (policy.AutoApproveSkills.Count > 0)
        {
            var skillIds = policy.AutoApproveSkills;
            var keys = await _databaseContext.Skills.AsNoTracking()
                .Where(x => skillIds.Contains(x.Id))
                .Select(x => x.Key)
                .ToListAsync(cancellationToken);
            names.AddRange(keys.Select(key => $"{MoAI.AI.AppToolApprovalContract.SkillToolPrefix}{key}"));
        }

        // 知识库/知识图谱：检索工具覆盖全部绑定资源，须全部命中白名单才免审批卡（与闸口判定一致）
        if (IsAllBoundApproved(AppAgentConfigJson.ParseLongList(snapshot?.WikiIds ?? config?.WikiIds), policy.AutoApproveWikis))
        {
            names.Add(MoAI.AI.AppToolApprovalContract.WikiSearchToolName);
            names.Add(MoAI.AI.AppToolApprovalContract.WikiChunkToolName);
        }

        if (IsAllBoundApproved(AppAgentConfigJson.ParseLongList(snapshot?.GraphIds ?? config?.GraphIds), policy.AutoApproveGraphs))
        {
            names.Add(MoAI.AI.AppToolApprovalContract.GraphSearchToolName);
        }

        return (names, prefixes);
    }

    /// <summary>
    /// 按生效绑定全量应用复现闸口装配的去重命名（{prefix}{应用名}，同名加短 id 后缀），把白名单应用的工具名加入清单.
    /// </summary>
    private async Task AddApprovedAppToolNamesAsync(List<string> names, IReadOnlyList<Guid> boundAppIds, IReadOnlyList<Guid> approvedIds, string prefix, CancellationToken cancellationToken)
    {
        if (approvedIds.Count == 0)
        {
            return;
        }

        var apps = await _databaseContext.Apps.AsNoTracking()
            .Where(x => boundAppIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name })
            .ToListAsync(cancellationToken);

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in apps)
        {
            var toolName = MoAI.AI.AppResourceToolNaming.PrefixedToolName(prefix, row.Name, row.Id, usedNames);
            if (toolName.Length > 0 && approvedIds.Contains(row.Id))
            {
                names.Add(toolName);
            }
        }
    }

    /// <summary>
    /// 多源检索工具放行判定：绑定列表非空且全部命中白名单.
    /// </summary>
    private static bool IsAllBoundApproved(IReadOnlyList<long> boundIds, IReadOnlyList<long> approvedIds)
        => boundIds.Count > 0 && boundIds.All(approvedIds.Contains);


    private static IReadOnlyList<Guid> ParseGuidList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<Guid>();
        }

        try
        {
            var raw = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            var result = new List<Guid>();
            foreach (var item in raw)
            {
                if (Guid.TryParse(item, out var id) && id != Guid.Empty)
                {
                    result.Add(id);
                }
            }

            return result;
        }
        catch (System.Text.Json.JsonException)
        {
            return Array.Empty<Guid>();
        }
    }
}
