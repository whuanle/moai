using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Workflow.Queries;
using MoAI.App.Workflow.Queries.Responses;
using MoAI.App.Workflow.Stores;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.App.Workflow.Handlers;

/// <summary>
/// <inheritdoc cref="QueryAppWorkflowConfigCommand"/>
/// </summary>
public class QueryAppWorkflowConfigCommandHandler : IRequestHandler<QueryAppWorkflowConfigCommand, QueryAppWorkflowConfigCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly DatabaseWorkflowDefinitionStore _definitionStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAppWorkflowConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="definitionStore">工作流定义存储.</param>
    public QueryAppWorkflowConfigCommandHandler(
        DatabaseContext databaseContext,
        ITeamService teamService,
        DatabaseWorkflowDefinitionStore definitionStore)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _definitionStore = definitionStore;
    }

    /// <inheritdoc/>
    public async Task<QueryAppWorkflowConfigCommandResponse> Handle(QueryAppWorkflowConfigCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps
            .Where(x => x.Id == request.AppId)
            .Select(x => new { x.Id, x.TeamId })
            .FirstOrDefaultAsync(cancellationToken);

        if (app == null)
        {
            throw new BusinessException("应用不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(app.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        var config = await _definitionStore.FindConfigEntityAsync(request.AppId, cancellationToken);

        // 开场白存于 app_agent_config（草稿/发布双轨）：编排定义与开场白均为「已发布且无草稿变更」时 status 才为 1
        var agentConfigStatus = await _databaseContext.AppAgentConfigs
            .Where(x => x.AppId == request.AppId)
            .Select(x => (int?)x.Status)
            .FirstOrDefaultAsync(cancellationToken);
        var combinedStatus = config?.Status == 1 && agentConfigStatus == 1 ? 1 : 0;

        return new QueryAppWorkflowConfigCommandResponse
        {
            AppId = request.AppId,
            ConfigId = config?.Id,
            DraftDefinition = config?.DraftDefinition,
            DraftEditorData = config?.DraftEditorData,
            PublishedDefinition = config?.PublishedDefinition,
            Version = config?.Version ?? 0,
            Status = (short)combinedStatus,
            PublishTime = config?.PublishTime,
        };
    }
}
