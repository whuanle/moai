using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.App.Commands.Responses;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Helper;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;

namespace MoAI.App.Handlers;

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
            IsPublic = false,
            IsDisable = false,
            Avatar = request.Avatar
        };

        var appCommonEntity = new AppCommonEntity
        {
            Id = Guid.CreateVersion7(),
            TeamId = request.TeamId,
            AppId = appId,
            Prompt = request.Prompt,
            ModelId = request.ModelId,
            WikiIds = request.WikiIds.ToJsonString(),
            Plugins = request.Plugins.ToJsonString(),
            ExecutionSettings = request.ExecutionSettings.ToJsonString(),
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
