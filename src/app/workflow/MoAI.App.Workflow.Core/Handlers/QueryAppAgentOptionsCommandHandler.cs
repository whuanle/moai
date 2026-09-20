using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Workflow.Queries;
using MoAI.App.Workflow.Queries.Responses;
using MoAI.App.Workflow.Services;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Team.Services;

namespace MoAI.App.Workflow.Handlers;

/// <summary>
/// <inheritdoc cref="QueryAppAgentOptionsCommand"/>
/// </summary>
public class QueryAppAgentOptionsCommandHandler : IRequestHandler<QueryAppAgentOptionsCommand, QueryAppAgentOptionsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAppAgentOptionsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public QueryAppAgentOptionsCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<QueryAppAgentOptionsCommandResponse> Handle(QueryAppAgentOptionsCommand request, CancellationToken cancellationToken)
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

        // 本团队已发布的内部 Agent 应用（未发布无法被流程调用，不列出）
        var agents = await _databaseContext.Apps.AsNoTracking()
            .Where(x => x.TeamId == app.TeamId
                && !x.IsExternal
                && !x.IsDisable
                && x.AppType == (int)AppType.Agent
                && x.PublishStatus == 1)
            .OrderByDescending(x => x.UpdateTime)
            .Select(x => new { x.Id, x.Name, x.Avatar })
            .ToListAsync(cancellationToken);

        var items = new List<AppAgentOptionItem>(agents.Count);
        foreach (var agent in agents)
        {
            items.Add(new AppAgentOptionItem
            {
                AppId = agent.Id,
                Name = agent.Name ?? string.Empty,
                AvatarPath = agent.Avatar ?? string.Empty,
                Circular = await AgentWorkflowCycleGuard.IsCircularAsync(_databaseContext, request.AppId, agent.Id, cancellationToken),
            });
        }

        return new QueryAppAgentOptionsCommandResponse { Items = items };
    }
}
