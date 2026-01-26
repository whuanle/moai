using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Workflow.Queries;
using MoAI.Workflow.Queries.Responses;

namespace MoAI.Workflow.Core.Queries;

/// <summary>
/// <inheritdoc cref="QueryWorkflowDefinitionCommand"/>
/// </summary>
public class QueryWorkflowDefinitionCommandHandler : IRequestHandler<QueryWorkflowDefinitionCommand, QueryWorkflowDefinitionCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWorkflowDefinitionCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryWorkflowDefinitionCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryWorkflowDefinitionCommandResponse> Handle(QueryWorkflowDefinitionCommand request, CancellationToken cancellationToken)
    {
        // 查询工作流定义
        var workflowDesign = await _databaseContext.WorkflowDesigns
            .Where(w => w.Id == request.Id && w.IsDeleted == 0)
            .FirstOrDefaultAsync(cancellationToken);

        // 如果不存在，抛出异常
        if (workflowDesign == null)
        {
            throw new BusinessException("工作流定义不存在") { StatusCode = 404 };
        }

        // 构建响应
        return new QueryWorkflowDefinitionCommandResponse
        {
            Id = workflowDesign.Id,
            TeamId = workflowDesign.TeamId,
            Name = workflowDesign.Name,
            Description = workflowDesign.Description,
            Avatar = workflowDesign.Avatar,
            UiDesign = workflowDesign.UiDesign,
            FunctionDesign = workflowDesign.FunctionDesgin,
            UiDesignDraft = request.IncludeDraft ? workflowDesign.UiDesignDraft : null,
            FunctionDesignDraft = request.IncludeDraft ? workflowDesign.FunctionDesignDraft : null,
            CreateTime = workflowDesign.CreateTime,
            UpdateTime = workflowDesign.UpdateTime,
            CreateUserId = workflowDesign.CreateUserId,
            UpdateUserId = workflowDesign.UpdateUserId
        };
    }
}
