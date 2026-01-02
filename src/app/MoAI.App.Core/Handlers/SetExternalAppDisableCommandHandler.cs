using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="SetExternalAppDisableCommand"/>
/// </summary>
public class SetExternalAppDisableCommandHandler : IRequestHandler<SetExternalAppDisableCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetExternalAppDisableCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public SetExternalAppDisableCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(SetExternalAppDisableCommand request, CancellationToken cancellationToken)
    {
        var entity = await _databaseContext.ExternalApps
            .FirstOrDefaultAsync(x => x.TeamId == request.TeamId, cancellationToken);

        if (entity == null)
        {
            throw new BusinessException("系统接入不存在") { StatusCode = 404 };
        }

        entity.IsDsiable = request.IsDisable;

        _databaseContext.ExternalApps.Update(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
