using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.Database;
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
            WikiIds = AppAgentConfigJson.ParseLongList(config?.WikiIds),
            Plugins = AppAgentConfigJson.ParseGuidList(config?.Plugins),
            Skills = AppAgentConfigJson.ParseGuidList(config?.Skills),
            ExecutionSettings = AppAgentConfigJson.ParseJsonObject(config?.ExecutionSettings),
            OpeningStatement = config?.OpeningStatement ?? string.Empty,
            OpeningStatementEnabled = config?.OpeningStatementEnabled ?? false,
            MyRole = (int)myRole.Value
        };
    }
}
