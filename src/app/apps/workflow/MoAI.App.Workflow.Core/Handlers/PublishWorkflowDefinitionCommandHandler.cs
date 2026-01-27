using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.System.Text.Json;
using MoAI.Workflow.Commands;
using MoAI.Workflow.Models;
using MoAI.Workflow.Services;

namespace MoAI.Workflow.Handlers;

/// <summary>
/// <inheritdoc cref="PublishWorkflowDefinitionCommand"/>
/// </summary>
public class PublishWorkflowDefinitionCommandHandler : IRequestHandler<PublishWorkflowDefinitionCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly WorkflowDefinitionService _workflowDefinitionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PublishWorkflowDefinitionCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="workflowDefinitionService">工作流定义服务.</param>
    public PublishWorkflowDefinitionCommandHandler(
        DatabaseContext databaseContext,
        WorkflowDefinitionService workflowDefinitionService)
    {
        _databaseContext = databaseContext;
        _workflowDefinitionService = workflowDefinitionService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(PublishWorkflowDefinitionCommand request, CancellationToken cancellationToken)
    {
        // 1. 检索工作流定义
        var workflowDesignEntity = await _databaseContext.WorkflowDesigns
            .FirstOrDefaultAsync(w => w.Id == request.Id && w.IsDeleted == 0, cancellationToken);

        if (workflowDesignEntity == null)
        {
            throw new BusinessException("工作流定义不存在") { StatusCode = 404 };
        }

        // 2. 检查草稿是否为空
        if (string.IsNullOrEmpty(workflowDesignEntity.FunctionDesignDraft))
        {
            throw new BusinessException("功能设计草稿为空，无法发布") { StatusCode = 400 };
        }

        // 3. 反序列化草稿
        WorkflowDefinition? definition;
        try
        {
            definition = JsonSerializer.Deserialize<WorkflowDefinition>(
                workflowDesignEntity.FunctionDesignDraft,
                JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);

            if (definition == null)
            {
                throw new BusinessException("草稿反序列化失败") { StatusCode = 400 };
            }
        }
        catch (JsonException ex)
        {
            throw new BusinessException($"草稿格式无效: {ex.Message}") { StatusCode = 400 };
        }

        // 4. 验证工作流定义
        try
        {
            _workflowDefinitionService.ValidateWorkflowDefinition(definition);
        }
        catch (BusinessException ex)
        {
            // 重新抛出验证错误，保持原有的错误信息
            throw new BusinessException($"工作流定义验证失败: {ex.Message}") { StatusCode = 400 };
        }

        // 5. 创建版本快照（保存当前已发布版本）
        if (!string.IsNullOrEmpty(workflowDesignEntity.FunctionDesgin))
        {
            // 计算新版本号
            var latestSnapshot = await _databaseContext.WorkflowDesginSnapshoots
                .Where(s => s.WorkflowDesginId == workflowDesignEntity.Id && s.IsDeleted == 0)
                .OrderByDescending(s => s.CreateTime)
                .FirstOrDefaultAsync(cancellationToken);

            var newVersion = latestSnapshot != null
                ? IncrementVersion(latestSnapshot.Version)
                : "1.0.0";

            var snapshot = new WorkflowDesginSnapshootEntity
            {
                Id = Guid.NewGuid(),
                WorkflowDesginId = workflowDesignEntity.Id,
                TeamId = workflowDesignEntity.TeamId,
                UiDesign = workflowDesignEntity.UiDesign,
                FunctionDesign = workflowDesignEntity.FunctionDesgin,
                Version = newVersion
            };

            await _databaseContext.WorkflowDesginSnapshoots.AddAsync(snapshot, cancellationToken);
        }

        // 6. 发布草稿（复制草稿到正式版本）
        workflowDesignEntity.UiDesign = workflowDesignEntity.UiDesignDraft;
        workflowDesignEntity.FunctionDesgin = workflowDesignEntity.FunctionDesignDraft;
        workflowDesignEntity.IsPublish = true;

        // 7. 保存更改
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }

    /// <summary>
    /// 递增版本号（简单的语义化版本递增）.
    /// </summary>
    /// <param name="currentVersion">当前版本号（例如 "1.0.0"）.</param>
    /// <returns>新版本号（例如 "1.0.1"）.</returns>
    private static string IncrementVersion(string currentVersion)
    {
        var parts = currentVersion.Split('.');
        if (parts.Length != 3)
        {
            return "1.0.0";
        }

        if (int.TryParse(parts[2], out var patch))
        {
            return $"{parts[0]}.{parts[1]}.{patch + 1}";
        }

        return "1.0.0";
    }
}
