using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Manager.ManagerApp.Models;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Helper;
using MoAI.Infra.Exceptions;

namespace MoAI.App.Manager.ManagerApp.Commands;

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
            Avatar = string.Empty
        };

        var appCommonEntity = new AppCommonEntity
        {
            Id = Guid.CreateVersion7(),
            TeamId = request.TeamId,
            AppId = appId,
            Prompt = string.Empty,
            ModelId = 0,
            WikiIds = "[]",
            Plugins = "[]",
            ExecutionSettings = "[]",
            IsAuth = false,
        };

        await _databaseContext.Apps.AddAsync(appEntity, cancellationToken);
        await _databaseContext.AppCommons.AddAsync(appCommonEntity, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        tran.Complete();

        return new CreateAppCommandResponse
        {
            AppId = appId
        };
    }
}
