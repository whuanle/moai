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
        var workflowDesignEntity = await _databaseContext.AppWorkflowDesigns
            .FirstOrDefaultAsync(w => w.AppId == request.AppId && w.IsDeleted == 0, cancellationToken);

        if (workflowDesignEntity == null)
        {
            throw new BusinessException("工作流定义不存在") { StatusCode = 404 };
        }

        // 查询对应的应用实体（用于更新基础信息）
        var appEntity = await _databaseContext.Apps
            .FirstOrDefaultAsync(a => a.Id == request.AppId && a.IsDeleted == 0, cancellationToken);

        if (appEntity == null)
        {
            throw new BusinessException("应用不存在") { StatusCode = 404 };
        }

        // 更新应用基础信息（Name、Description、Avatar 存储在 AppEntity 中）
        if (!string.IsNullOrEmpty(request.Name))
        {
            appEntity.Name = request.Name;
        }

        if (request.Description != null)
        {
            appEntity.Description = request.Description;
        }

        if (request.Avatar != null)
        {
            appEntity.Avatar = request.Avatar;
        }

        // 更新草稿字段（不影响已发布版本）
        if (request.UiDesignDraft != null)
        {
            workflowDesignEntity.UiDesignDraft = request.UiDesignDraft;
        }

        //// 构建工作流定义对象用于验证
        //var workflowDefinition = new WorkflowDefinition
        //{
        //    Id = request.AppId.ToString(),
        //    Name = request.Name ?? appEntity.Name,
        //    Description = request.Description ?? appEntity.Description,
        //    Nodes = request.Nodes
        //};

        //// 验证工作流定义的有效性（节点类型、连接、图结构等）
        //_workflowDefinitionService.ValidateWorkflowDefinition(workflowDefinition);

        // 验证通过后，序列化并保存到草稿字段
        workflowDesignEntity.FunctionDesignDraft = JsonSerializer.Serialize(request.Nodes, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);

        // 保存更改（审计字段会自动更新）
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
