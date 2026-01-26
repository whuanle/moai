using MediatR;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Models;
using MoAI.Workflow.Commands;

namespace MoAI.Workflow.Handlers;

/// <summary>
/// <inheritdoc cref="CreateWorkflowDefinitionCommand"/>
/// </summary>
public class CreateWorkflowDefinitionCommandHandler : IRequestHandler<CreateWorkflowDefinitionCommand, SimpleGuid>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateWorkflowDefinitionCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public CreateWorkflowDefinitionCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<SimpleGuid> Handle(CreateWorkflowDefinitionCommand request, CancellationToken cancellationToken)
    {
        // 创建工作流设计实体（只包含基础信息）
        var workflowDesignEntity = new WorkflowDesignEntity
        {
            Id = Guid.NewGuid(),
            TeamId = request.TeamId,
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            Avatar = request.Avatar ?? string.Empty,
            UiDesign = string.Empty,
            FunctionDesgin = string.Empty,
            UiDesignDraft = string.Empty,
            FunctionDesignDraft = string.Empty,
            IsPublish = false
        };

        // 存储到数据库
        await _databaseContext.WorkflowDesigns.AddAsync(workflowDesignEntity, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        // 返回工作流定义 ID
        return new SimpleGuid
        {
            Value = workflowDesignEntity.Id
        };
    }
}
