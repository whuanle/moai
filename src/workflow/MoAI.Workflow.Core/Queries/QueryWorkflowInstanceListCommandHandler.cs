using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Workflow.Queries;
using MoAI.Workflow.Queries.Responses;

namespace MoAI.Workflow.Core.Queries;

/// <summary>
/// <inheritdoc cref="QueryWorkflowInstanceListCommand"/>
/// </summary>
public class QueryWorkflowInstanceListCommandHandler : IRequestHandler<QueryWorkflowInstanceListCommand, QueryWorkflowInstanceListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWorkflowInstanceListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryWorkflowInstanceListCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryWorkflowInstanceListCommandResponse> Handle(
        QueryWorkflowInstanceListCommand request,
        CancellationToken cancellationToken)
    {
        var query = _databaseContext.WorkflowHistories
            .Where(w => w.IsDeleted == 0);

        // 应用过滤条件
        if (request.WorkflowDesignId.HasValue)
        {
            query = query.Where(w => w.WorkflowDesignId == request.WorkflowDesignId.Value);
        }

        if (request.TeamId.HasValue)
        {
            query = query.Where(w => w.TeamId == request.TeamId.Value);
        }

        if (request.State.HasValue)
        {
            query = query.Where(w => w.State == request.State.Value);
        }

        // 获取总数
        var totalCount = await query.CountAsync(cancellationToken);

        // 分页查询（页码从 1 开始，需要转换为从 0 开始）
        var pageIndex = Math.Max(0, request.PageIndex - 1);
        var items = await query
            .OrderByDescending(w => w.CreateTime)
            .Skip(pageIndex * request.PageSize)
            .Take(request.PageSize)
            .Join(
                _databaseContext.WorkflowDesigns,
                history => history.WorkflowDesignId,
                design => design.Id,
                (history, design) => new QueryWorkflowInstanceListCommandResponseItem
                {
                    Id = history.Id,
                    TeamId = history.TeamId,
                    WorkflowDesignId = history.WorkflowDesignId,
                    WorkflowName = design.Name,
                    State = history.State,
                    CreateTime = history.CreateTime,
                    UpdateTime = history.UpdateTime,
                    CreateUserId = history.CreateUserId
                })
            .ToListAsync(cancellationToken);

        return new QueryWorkflowInstanceListCommandResponse
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }
}
