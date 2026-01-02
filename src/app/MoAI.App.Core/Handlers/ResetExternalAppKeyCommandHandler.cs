using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.App.Commands.Responses;
using MoAI.Database;
using MoAI.Infra.Exceptions;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="ResetExternalAppKeyCommand"/>
/// </summary>
public class ResetExternalAppKeyCommandHandler : IRequestHandler<ResetExternalAppKeyCommand, ResetExternalAppKeyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResetExternalAppKeyCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public ResetExternalAppKeyCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<ResetExternalAppKeyCommandResponse> Handle(ResetExternalAppKeyCommand request, CancellationToken cancellationToken)
    {
        var entity = await _databaseContext.ExternalApps
            .FirstOrDefaultAsync(x => x.TeamId == request.TeamId, cancellationToken);

        if (entity == null)
        {
            throw new BusinessException("系统接入不存在") { StatusCode = 404 };
        }

        var newKey = Guid.NewGuid().ToString("N");
        entity.Key = newKey;

        _databaseContext.ExternalApps.Update(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new ResetExternalAppKeyCommandResponse
        {
            Key = newKey
        };
    }
}
