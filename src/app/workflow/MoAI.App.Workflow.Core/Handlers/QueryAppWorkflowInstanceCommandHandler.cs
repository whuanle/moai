using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Workflow.Queries;
using MoAI.App.Workflow.Queries.Responses;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.App.Workflow.Handlers;

/// <summary>
/// <inheritdoc cref="QueryAppWorkflowInstanceCommand"/>
/// </summary>
public class QueryAppWorkflowInstanceCommandHandler : IRequestHandler<QueryAppWorkflowInstanceCommand, QueryAppWorkflowInstanceCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAppWorkflowInstanceCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public QueryAppWorkflowInstanceCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<QueryAppWorkflowInstanceCommandResponse> Handle(QueryAppWorkflowInstanceCommand request, CancellationToken cancellationToken)
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

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("需要团队管理员才能查看运行详情.") { StatusCode = 403 };
        }

        var entity = await _databaseContext.AppWorkflowInstances
            .FirstOrDefaultAsync(x => x.Id == request.InstanceId && x.AppId == app.Id, cancellationToken);

        if (entity == null)
        {
            throw new BusinessException("运行实例不存在.") { StatusCode = 404 };
        }

        var instance = JsonSerializer.Deserialize<Instance.WorkflowInstance>(entity.InstanceData, WorkflowJson.Options);

        return new QueryAppWorkflowInstanceCommandResponse
        {
            InstanceId = entity.Id,
            AppId = entity.AppId,
            Status = entity.Status,
            IsDebug = entity.IsDebug,
            Version = entity.Version,
            Input = entity.Input,
            Output = entity.Output,
            ErrorMessage = entity.ErrorMessage,
            StartTime = entity.StartTime,
            EndTime = entity.EndTime,
            Nodes = instance?.NodeStates.Values
                .OrderBy(n => n.StartedAt ?? DateTimeOffset.MaxValue)
                .Select(n => new WorkflowNodeExecution
                {
                    NodeKey = n.NodeKey,
                    NodeType = n.NodeType,
                    NodeName = n.NodeName,
                    State = n.State.ToString().ToLowerInvariant(),
                    Input = n.Input?.ToJsonString(),
                    Output = n.Output?.ToJsonString(),
                    ErrorMessage = n.ErrorMessage,
                    Attempts = n.Attempts,
                    StartedAt = n.StartedAt,
                    EndedAt = n.EndedAt,
                })
                .ToList() ?? new List<WorkflowNodeExecution>(),
        };
    }
}
