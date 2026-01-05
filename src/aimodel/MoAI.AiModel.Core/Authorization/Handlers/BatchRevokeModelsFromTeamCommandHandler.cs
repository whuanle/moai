using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AiModel.Authorization.Commands;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.AiModel.Authorization.Handlers;

/// <summary>
/// <inheritdoc cref="BatchRevokeModelsFromTeamCommand"/>
/// </summary>
public class BatchRevokeModelsFromTeamCommandHandler : IRequestHandler<BatchRevokeModelsFromTeamCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchRevokeModelsFromTeamCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    public BatchRevokeModelsFromTeamCommandHandler(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(BatchRevokeModelsFromTeamCommand request, CancellationToken cancellationToken)
    {
        var team = await _dbContext.Teams
            .FirstOrDefaultAsync(x => x.Id == request.TeamId, cancellationToken);

        if (team == null)
        {
            throw new BusinessException("团队不存在") { StatusCode = 404 };
        }

        var authsToRemove = await _dbContext.AiModelAuthorizations
            .Where(x => x.TeamId == request.TeamId && request.ModelIds.Contains(x.AiModelId))
            .ToListAsync(cancellationToken);

        if (authsToRemove.Count > 0)
        {
            _dbContext.AiModelAuthorizations.RemoveRange(authsToRemove);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return EmptyCommandResponse.Default;
    }
}
