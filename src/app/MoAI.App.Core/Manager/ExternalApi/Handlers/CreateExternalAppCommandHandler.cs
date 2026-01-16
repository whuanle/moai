using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Manager.ExternalApi.Models;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;

namespace MoAI.App.Manager.ExternalApi.Commands;

/// <summary>
/// <inheritdoc cref="CreateExternalAppCommand"/>
/// </summary>
public class CreateExternalAppCommandHandler : IRequestHandler<CreateExternalAppCommand, CreateExternalAppCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateExternalAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public CreateExternalAppCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<CreateExternalAppCommandResponse> Handle(CreateExternalAppCommand request, CancellationToken cancellationToken)
    {
        // 检查团队是否已有系统接入
        var existingApp = await _databaseContext.ExternalApps
            .AnyAsync(x => x.TeamId == request.TeamId, cancellationToken);

        if (existingApp)
        {
            throw new BusinessException("该团队已存在系统接入，不能重复创建") { StatusCode = 409 };
        }

        var appId = Guid.CreateVersion7();
        var key = Guid.NewGuid().ToString("N");

        var entity = new ExternalAppEntity
        {
            Id = appId,
            TeamId = request.TeamId,
            Name = request.Name,
            Description = request.Description,
            Avatar = request.Avatar,
            Key = key,
            IsDsiable = false,
        };

        await _databaseContext.ExternalApps.AddAsync(entity, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new CreateExternalAppCommandResponse
        {
            AppId = appId,
            Key = key
        };
    }
}
