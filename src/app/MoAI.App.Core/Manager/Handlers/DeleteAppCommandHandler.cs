using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Manager.Commands;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.App.Manager.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteAppCommand"/>
/// </summary>
public class DeleteAppCommandHandler : IRequestHandler<DeleteAppCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public DeleteAppCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteAppCommand request, CancellationToken cancellationToken)
    {
        var appEntity = await _databaseContext.Apps
            .FirstOrDefaultAsync(x => x.Id == request.AppId && x.TeamId == request.TeamId, cancellationToken);

        if (appEntity == null)
        {
            throw new BusinessException("应用不存在") { StatusCode = 404 };
        }

        // 软删除应用实体
        _databaseContext.Apps.Remove(appEntity);

        // 如果是 Workflow 类型的应用，同时软删除关联的 AppWorkflowDesignEntity
        if (appEntity.AppType == (int)App.Models.AppType.Workflow)
        {
            var workflowDesignEntity = await _databaseContext.AppWorkflowDesigns
                .FirstOrDefaultAsync(w => w.AppId == request.AppId && w.IsDeleted == 0, cancellationToken);

            if (workflowDesignEntity != null)
            {
                _databaseContext.AppWorkflowDesigns.Remove(workflowDesignEntity);
            }
        }

        // 注意：AppWorkflowHistoryEntity 执行历史不会被删除，保留以供审计
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
