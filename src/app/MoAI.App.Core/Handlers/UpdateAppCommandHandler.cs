using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateAppCommand"/>
/// </summary>
public class UpdateAppCommandHandler : IRequestHandler<UpdateAppCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public UpdateAppCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateAppCommand request, CancellationToken cancellationToken)
    {
        var appEntity = await _databaseContext.Apps
            .FirstOrDefaultAsync(x => x.Id == request.AppId && x.TeamId == request.TeamId, cancellationToken);

        if (appEntity == null)
        {
            throw new BusinessException("应用不存在") { StatusCode = 404 };
        }

        var appCommonEntity = await _databaseContext.AppCommons
            .FirstOrDefaultAsync(x => x.AppId == request.AppId && x.TeamId == request.TeamId, cancellationToken);

        if (appCommonEntity == null)
        {
            throw new BusinessException("应用配置不存在") { StatusCode = 404 };
        }

        // 更新基础信息
        appEntity.Name = request.Name;
        appEntity.Description = request.Description;
        appEntity.IsPublic = request.IsPublic;

        // 更新配置信息
        appEntity.Avatar = request.Avatar;
        appCommonEntity.Prompt = request.Prompt;
        appCommonEntity.ModelId = request.ModelId;
        appCommonEntity.WikiIds = request.WikiIds.ToJsonString();
        appCommonEntity.Plugins = request.Plugins.ToJsonString();
        appCommonEntity.ExecutionSettings = request.ExecutionSettings.ToJsonString();
        appCommonEntity.IsAuth = request.IsAuth;

        _databaseContext.Apps.Update(appEntity);
        _databaseContext.AppCommons.Update(appCommonEntity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
