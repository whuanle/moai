using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateExternalAppCommand"/>
/// </summary>
public class UpdateExternalAppCommandHandler : IRequestHandler<UpdateExternalAppCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateExternalAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public UpdateExternalAppCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateExternalAppCommand request, CancellationToken cancellationToken)
    {
        var entity = await _databaseContext.ExternalApps
            .FirstOrDefaultAsync(x => x.TeamId == request.TeamId, cancellationToken);

        if (entity == null)
        {
            throw new BusinessException("系统接入不存在") { StatusCode = 404 };
        }

        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.Avatar = request.Avatar;

        _databaseContext.ExternalApps.Update(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
