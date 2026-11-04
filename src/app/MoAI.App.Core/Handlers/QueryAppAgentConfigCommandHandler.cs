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
            MyRole = (int)myRole.Value
        };
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
