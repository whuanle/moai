using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.System.Text.Json;
using MoAI.Workflow.Commands;
using MoAI.Workflow.Models;
using MoAI.Workflow.Services;

namespace MoAI.Workflow.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateWorkflowDefinitionCommand"/>
/// </summary>
public class UpdateWorkflowDefinitionCommandHandler : IRequestHandler<UpdateWorkflowDefinitionCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly WorkflowDefinitionService _workflowDefinitionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateWorkflowDefinitionCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="workflowDefinitionService">工作流定义验证服务.</param>
    public UpdateWorkflowDefinitionCommandHandler(
        DatabaseContext databaseContext,
        WorkflowDefinitionService workflowDefinitionService)
    {
        _databaseContext = databaseContext;
        _workflowDefinitionService = workflowDefinitionService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateWorkflowDefinitionCommand request, CancellationToken cancellationToken)
    {
        // 查询现有的工作流设计实体
        var workflowDesignEntity = await _databaseContext.WorkflowDesigns
            .FirstOrDefaultAsync(w => w.Id == request.Id && w.IsDeleted == 0, cancellationToken);

        if (workflowDesignEntity == null)
        {
            throw new BusinessException("工作流定义不存在") { StatusCode = 404 };
        }

        // 部分更新：只更新提供的字段
        if (!string.IsNullOrEmpty(request.Name))
        {
            workflowDesignEntity.Name = request.Name;
        }

        if (request.Description != null)
        {
            workflowDesignEntity.Description = request.Description;
        }

        if (request.Avatar != null)
        {
            workflowDesignEntity.Avatar = request.Avatar;
        }

        // 更新草稿字段（不影响已发布版本）
        if (request.UiDesignDraft != null)
        {
            workflowDesignEntity.UiDesignDraft = JsonSerializer.Serialize(
                request.UiDesignDraft,
                JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
        }

        // 如果提供了 Nodes 和 Connections，则验证并更新功能设计草稿
        if (request.Nodes != null && request.Connections != null)
        {
            // 构建 WorkflowDefinition 对象用于验证
            var workflowDefinition = new WorkflowDefinition
            {
                Nodes = request.Nodes,
                Connections = request.Connections
            };

            // 验证工作流定义的有效性（节点类型、连接、图结构等）
            _workflowDefinitionService.ValidateWorkflowDefinition(workflowDefinition);

            // 验证通过后，序列化并保存到草稿字段
            workflowDesignEntity.FunctionDesignDraft = JsonSerializer.Serialize(
                workflowDefinition,
                JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
        }

        // 保存更改（审计字段会自动更新）
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
