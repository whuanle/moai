using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="QueryAppAgentConfigCommand"/>
/// </summary>
public class QueryAppAgentConfigCommandHandler : IRequestHandler<QueryAppAgentConfigCommand, QueryAppAgentConfigCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAppAgentConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public QueryAppAgentConfigCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<QueryAppAgentConfigCommandResponse> Handle(QueryAppAgentConfigCommand request, CancellationToken cancellationToken)
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

        // 配置行可能尚未创建（应用刚建、未保存过配置），此时返回空配置而非 404
        var config = await _databaseContext.AppAgentConfigs
            .FirstOrDefaultAsync(x => x.AppId == app.Id, cancellationToken);

        // 绑定的团队智能体选项：按绑定顺序回显实时行（含已停用项，前端标记重新选择）
        var teamAgentIds = AppAgentConfigJson.ParseGuidList(config?.TeamAgentIds);
        var teamAgents = await ResolveTeamAgentOptionsAsync(teamAgentIds, cancellationToken);

        return new QueryAppAgentConfigCommandResponse
        {
            AppId = app.Id,
            TeamId = app.TeamId,
            AppType = (AppType)app.AppType,
            Prompt = config?.Prompt ?? string.Empty,
            ModelId = config?.ModelId ?? Guid.Empty,
            ModelIds = ResolveModelIds(config),
            WikiIds = AppAgentConfigJson.ParseLongList(config?.WikiIds),
            GraphIds = AppAgentConfigJson.ParseLongList(config?.GraphIds),
            Plugins = AppAgentConfigJson.ParseGuidList(config?.Plugins),
            WorkflowApps = AppAgentConfigJson.ParseGuidList(config?.WorkflowApps),
            AgentApps = AppAgentConfigJson.ParseGuidList(config?.AgentApps),
            Skills = AppAgentConfigJson.ParseGuidList(config?.Skills),
            ExecutionSettings = AppAgentConfigJson.ParseJsonObject(config?.ExecutionSettings),
            OpeningStatement = config?.OpeningStatement ?? string.Empty,
            OpeningStatementEnabled = config?.OpeningStatementEnabled ?? false,
            QuickInputs = AppAgentConfigJson.ParseStringList(config?.QuickInputs),
            Status = config?.Status ?? 0,
            TeamAgentIds = teamAgentIds,
            DefaultTeamAgentId = config?.DefaultTeamAgentId,
            TeamAgents = teamAgents,
            MyRole = (int)myRole.Value
        };
    }

    /// <summary>
    /// 按绑定顺序解析团队智能体选项（实时行，含已停用项）；已删除的绑定 id 静默丢弃.
    /// </summary>
    /// <param name="teamAgentIds">绑定 id 列表（顺序即回显顺序）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>选项列表.</returns>
    private async Task<IReadOnlyList<AppTeamAgentOption>> ResolveTeamAgentOptionsAsync(IReadOnlyList<Guid> teamAgentIds, CancellationToken cancellationToken)
    {
        if (teamAgentIds.Count == 0)
        {
            return new List<AppTeamAgentOption>();
        }

        var rows = await _databaseContext.TeamAgents
            .Where(x => teamAgentIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Key, x.Name, x.Description, x.AvatarObjectKey, x.Enabled, x.MainPluginIds, x.MainWikiIds, x.MainGraphIds })
            .ToListAsync(cancellationToken);

        var rowMap = rows.ToDictionary(x => x.Id);
        var result = new List<AppTeamAgentOption>();
        foreach (var id in teamAgentIds)
        {
            if (!rowMap.TryGetValue(id, out var row))
            {
                continue;
            }

            result.Add(new AppTeamAgentOption
            {
                Id = row.Id,
                Key = row.Key,
                Name = row.Name,
                Description = row.Description ?? string.Empty,
                AvatarObjectKey = row.AvatarObjectKey ?? string.Empty,
                Enabled = row.Enabled,
                // 弱依赖声明随选项下发：前端对比应用绑定提示「一键添加」补齐（不阻断绑定）
                MainPluginIds = AppAgentConfigJson.ParseGuidList(row.MainPluginIds),
                MainWikiIds = AppAgentConfigJson.ParseLongList(row.MainWikiIds),
                MainGraphIds = AppAgentConfigJson.ParseLongList(row.MainGraphIds),
            });
        }

        return result;
    }

    /// <summary>
    /// 解析可选模型列表回显：未配置时仅含默认模型；存量行 model_ids 为空时归一为 [model_id].
    /// </summary>
    /// <param name="config">配置行，可能为 null.</param>
    /// <returns>可选模型 id 列表.</returns>
    private static IReadOnlyList<Guid> ResolveModelIds(AppAgentConfigEntity? config)
    {
        if (config == null)
        {
            return new List<Guid>();
        }

        var ids = AppAgentConfigJson.ParseGuidList(config.ModelIds);
        if (ids.Count == 0)
        {
            ids = new List<Guid> { config.ModelId };
        }

        return ids;
    }
}
