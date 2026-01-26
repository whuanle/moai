using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Chat.Manager.Commands;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;

namespace MoAI.App.Manager.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateChatAppCommand"/>
/// </summary>
public class UpdateChatAppCommandHandler : IRequestHandler<UpdateChatAppCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateChatAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public UpdateChatAppCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateChatAppCommand request, CancellationToken cancellationToken)
    {
        // todo：要检查这个团队能不能使用这些模型、知识库和插件
        var appEntity = await _databaseContext.Apps
            .FirstOrDefaultAsync(x => x.Id == request.AppId && x.TeamId == request.TeamId, cancellationToken);

        if (appEntity == null)
        {
            throw new BusinessException("应用不存在") { StatusCode = 404 };
        }

        var appCommonEntity = await _databaseContext.AppChatapps
            .FirstOrDefaultAsync(x => x.AppId == request.AppId && x.TeamId == request.TeamId, cancellationToken);

        if (appCommonEntity == null)
        {
            throw new BusinessException("应用配置不存在") { StatusCode = 404 };
        }

        // 更新基础信息
        appEntity.Name = request.Name;
        appEntity.Description = request.Description;
        appEntity.IsPublic = request.IsPublic;
        appEntity.ClassifyId = request.ClassifyId;

        if (appEntity.IsForeign)
        {
            appEntity.IsAuth = request.IsAuth;
        }

        // 更新配置信息
        appEntity.Avatar = request.Avatar;
        appCommonEntity.Prompt = request.Prompt;
        appCommonEntity.ModelId = request.ModelId;
        appCommonEntity.WikiIds = request.WikiIds.ToJsonString();
        appCommonEntity.Plugins = request.Plugins.ToJsonString();
        appCommonEntity.ExecutionSettings = request.ExecutionSettings.ToJsonString();

        _databaseContext.Apps.Update(appEntity);
        _databaseContext.AppChatapps.Update(appCommonEntity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
