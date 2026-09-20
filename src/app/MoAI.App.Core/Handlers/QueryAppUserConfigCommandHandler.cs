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
            .Select(x => new { x.Skills, x.ExecutionSettings, x.PublishedConfig })
            .FirstOrDefaultAsync(cancellationToken);

        var defaultSkillIds = ParseGuidList(config?.Skills);

        // 应用默认技能目录（按配置顺序）；技能被删除/禁用时目录静默缺失，用户勾选随之失效
        var defaultSkills = new List<AppUserSkillOption>();
        if (defaultSkillIds.Count > 0)
        {
            var rows = await _databaseContext.Skills.AsNoTracking()
                .Where(x => defaultSkillIds.Contains(x.Id) && !x.IsDisable)
                .Select(x => new { x.Id, x.Key, x.Name, x.Description, x.IsSystem, x.TeamId })
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

        // 审批策略按发布快照解析（与对话闸口一致）：白名单插件与沙箱在审批模式下自动放行，前端据此免展示审批卡
        var (autoNames, autoPrefixes) = await ResolveAutoApprovedToolsAsync(app, config?.PublishedConfig, config?.ExecutionSettings, cancellationToken);

        return new QueryAppUserConfigCommandResponse
        {
            PromptId = userConfig?.PromptId ?? 0,
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
    /// 解析审批策略自动放行的工具名/前缀：已发布应用按发布快照的执行参数（与闸口一致），未发布按实时草稿；
    /// 白名单插件解析为工具名（原生=插件名，MCP/OpenAPI=插件名__函数名），沙箱开关解析为 sandbox_ 前缀.
    /// </summary>
    private async Task<(List<string> Names, List<string> Prefixes)> ResolveAutoApprovedToolsAsync(
        AppEntity app, string? publishedConfig, string? draftExecutionSettings, CancellationToken cancellationToken)
    {
        var executionSettings = app.PublishStatus == 1
            ? AppAgentConfigSnapshot.TryParse(publishedConfig)?.ExecutionSettings ?? draftExecutionSettings
            : draftExecutionSettings;
        var policy = MoAI.AI.AppToolApprovalPolicy.Parse(executionSettings);

        var prefixes = new List<string>();
        if (policy.SandboxAutoApproved)
        {
            prefixes.Add(MoAI.AI.AppToolApprovalContract.SandboxToolPrefix);
        }

        var names = new List<string>();
        if (policy.AutoApprovePlugins.Count > 0)
        {
            var ids = policy.AutoApprovePlugins;
            var plugins = await _databaseContext.Plugins.AsNoTracking()
                .Where(x => ids.Contains(x.Id))
                .Select(x => new { x.PluginId, x.PluginName, x.Type })
                .ToListAsync(cancellationToken);

            // 原生插件（静态/动态）一个插件一个工具，工具名即插件名
            names.AddRange(plugins
                .Where(x => x.Type == (int)PluginType.NativePlugin)
                .Select(x => x.PluginName));

            // 自定义插件（MCP/OpenAPI）按函数展开为 {插件名}__{函数名}
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

        return (names, prefixes);
    }

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
