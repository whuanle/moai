using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Manager.Commands;
using MoAI.App.Manager.Models;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Helper;
using MoAI.Infra.Exceptions;

namespace MoAI.App.Manager.Handlers;

/// <summary>
/// <inheritdoc cref="CreateAppCommand"/>
/// </summary>
public class CreateAppCommandHandler : IRequestHandler<CreateAppCommand, CreateAppCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public CreateAppCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<CreateAppCommandResponse> Handle(CreateAppCommand request, CancellationToken cancellationToken)
    {
        // 检查应用名称重复
        var existingApp = await _databaseContext.Apps
            .AnyAsync(a => a.TeamId == request.TeamId && a.Name == request.Name, cancellationToken);
        if (existingApp)
        {
            throw new BusinessException("应用名称已存在");
        }

        using var tran = TransactionScopeHelper.Create();

        var appId = Guid.CreateVersion7();

        var appEntity = new AppEntity
        {
            Id = appId,
            TeamId = request.TeamId,
            Name = request.Name,
            Description = request.Description,
            IsForeign = request.IsForeign,
            AppType = (int)request.AppType,
            ClassifyId = request.ClassifyId,
            IsPublic = false,
            IsDisable = false,
            Avatar = string.Empty,
            IsAuth = false,
        };

        await _databaseContext.Apps.AddAsync(appEntity, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        if (request.AppType == App.Models.AppType.Chat)
        {
            var appChatappEntity = new AppChatappEntity
            {
                Id = Guid.CreateVersion7(),
                TeamId = request.TeamId,
                AppId = appId,
                Prompt = string.Empty,
                ModelId = 0,
                WikiIds = "[]",
                Plugins = "[]",
                ExecutionSettings = "[]",
            };
            await _databaseContext.AppChatapps.AddAsync(appChatappEntity, cancellationToken);
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }
        else if (request.AppType == App.Models.AppType.Workflow)
        {
            // 创建工作流设计实体
            var workflowDesignEntity = new AppWorkflowDesignEntity
            {
                Id = Guid.CreateVersion7(),
                TeamId = request.TeamId,
                AppId = appId,  // 关联到 AppEntity
                UiDesign = string.Empty,
                FunctionDesgin = string.Empty,
                UiDesignDraft = string.Empty,
                FunctionDesignDraft = string.Empty,
                IsPublish = false
            };

            // 存储到数据库
            await _databaseContext.AppWorkflowDesigns.AddAsync(workflowDesignEntity, cancellationToken);
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }

        tran.Complete();

        return new CreateAppCommandResponse
        {
            AppId = appId
        };
    }
}
