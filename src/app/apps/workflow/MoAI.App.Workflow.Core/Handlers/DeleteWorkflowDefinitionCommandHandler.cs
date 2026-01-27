using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Workflow.Commands;

namespace MoAI.Workflow.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteWorkflowDefinitionCommand"/>
/// </summary>
public class DeleteWorkflowDefinitionCommandHandler : IRequestHandler<DeleteWorkflowDefinitionCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWorkflowDefinitionCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public DeleteWorkflowDefinitionCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteWorkflowDefinitionCommand request, CancellationToken cancellationToken)
    {
        // 查询现有的工作流设计实体
        var workflowDesignEntity = await _databaseContext.WorkflowDesigns
            .FirstOrDefaultAsync(w => w.Id == request.Id && w.IsDeleted == 0, cancellationToken);

        if (workflowDesignEntity == null)
        {
            throw new BusinessException(404, $"工作流定义不存在: {request.Id}");
        }

        // 保存更改
        // 执行历史（WorkflowHistoryEntity）不会被删除，保留以供审计
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
