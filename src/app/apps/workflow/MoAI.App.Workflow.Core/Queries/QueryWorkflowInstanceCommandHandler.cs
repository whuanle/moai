using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Workflow.Queries;
using MoAI.Workflow.Queries.Responses;

namespace MoAI.Workflow.Core.Queries;

/// <summary>
/// <inheritdoc cref="QueryWorkflowInstanceCommand"/>
/// </summary>
public class QueryWorkflowInstanceCommandHandler : IRequestHandler<QueryWorkflowInstanceCommand, QueryWorkflowInstanceCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWorkflowInstanceCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryWorkflowInstanceCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryWorkflowInstanceCommandResponse> Handle(QueryWorkflowInstanceCommand request, CancellationToken cancellationToken)
    {
        // 查询工作流执行历史
        var workflowHistory = await _databaseContext.AppWorkflowHistories
            .Where(w => w.Id == request.Id && w.IsDeleted == 0)
            .FirstOrDefaultAsync(cancellationToken);

        // 如果不存在，抛出异常
        if (workflowHistory == null)
        {
            throw new BusinessException("工作流实例不存在") { StatusCode = 404 };
        }

        // 构建响应
        return new QueryWorkflowInstanceCommandResponse
        {
            Id = workflowHistory.Id,
            TeamId = workflowHistory.TeamId,
            WorkflowDesignId = workflowHistory.WorkflowDesignId,
            State = workflowHistory.State,
            SystemParameters = workflowHistory.SystemParamters,
            RunParameters = workflowHistory.RunParamters,
            Data = workflowHistory.Data,
            CreateTime = workflowHistory.CreateTime,
            UpdateTime = workflowHistory.UpdateTime,
            CreateUserId = workflowHistory.CreateUserId
        };
    }
}
