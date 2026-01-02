using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.App.Handlers;

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

        var appCommonEntity = await _databaseContext.AppCommons
            .FirstOrDefaultAsync(x => x.AppId == request.AppId && x.TeamId == request.TeamId, cancellationToken);

        _databaseContext.Apps.Remove(appEntity);
        if (appCommonEntity != null)
        {
            _databaseContext.AppCommons.Remove(appCommonEntity);
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
