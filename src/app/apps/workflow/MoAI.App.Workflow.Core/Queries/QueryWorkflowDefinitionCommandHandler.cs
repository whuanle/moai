using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Models;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Workflow.Models;
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
        // 查询应用实体（获取基础信息）
        var appEntity = await _databaseContext.Apps
            .Where(a => a.Id == request.AppId && a.IsDeleted == 0)
            .FirstOrDefaultAsync(cancellationToken);

        if (appEntity == null)
        {
            throw new BusinessException("应用不存在") { StatusCode = 404 };
        }

        // 验证是否为 Workflow 类型
        if (appEntity.AppType != (int)AppType.Workflow)
        {
            throw new BusinessException("该应用不是工作流类型") { StatusCode = 400 };
        }

        // 查询工作流设计实体（获取设计数据）
        var workflowDesign = await _databaseContext.AppWorkflowDesigns
            .Where(w => w.AppId == request.AppId && w.IsDeleted == 0)
            .FirstOrDefaultAsync(cancellationToken);

        if (workflowDesign == null)
        {
            throw new BusinessException("工作流定义不存在") { StatusCode = 404 };
        }

        // 构建响应（基础信息来自 AppEntity，设计数据来自 AppWorkflowDesignEntity）
        return new QueryWorkflowDefinitionCommandResponse
        {
            Id = workflowDesign.Id,
            AppId = appEntity.Id,
            TeamId = appEntity.TeamId,
            Name = appEntity.Name,
            Description = appEntity.Description,
            Avatar = appEntity.Avatar,
            UiDesignDraft = workflowDesign.UiDesignDraft,
            FunctionDesignDraft = workflowDesign.FunctionDesignDraft.JsonToObject<IReadOnlyCollection<NodeDesign>>()!,
            IsPublish = workflowDesign.IsPublish,
            CreateTime = appEntity.CreateTime,
            UpdateTime = appEntity.UpdateTime,
            CreateUserId = appEntity.CreateUserId,
            UpdateUserId = appEntity.UpdateUserId
        };
    }
}
