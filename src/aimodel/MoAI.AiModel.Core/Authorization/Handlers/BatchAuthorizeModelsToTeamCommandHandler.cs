using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AiModel.Authorization.Commands;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.AiModel.Authorization.Handlers;

/// <summary>
/// <inheritdoc cref="BatchAuthorizeModelsToTeamCommand"/>
/// </summary>
public class BatchAuthorizeModelsToTeamCommandHandler : IRequestHandler<BatchAuthorizeModelsToTeamCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchAuthorizeModelsToTeamCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    public BatchAuthorizeModelsToTeamCommandHandler(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(BatchAuthorizeModelsToTeamCommand request, CancellationToken cancellationToken)
    {
        var team = await _dbContext.Teams
            .FirstOrDefaultAsync(x => x.Id == request.TeamId, cancellationToken);

        if (team == null)
        {
            throw new BusinessException("团队不存在") { StatusCode = 404 };
        }

        var aimodelIds = request.ModelIds.ToHashSet();

        var existAiModelIds = await _dbContext.AiModels
            .Where(x => aimodelIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var notExistIds = aimodelIds.Except(existAiModelIds).ToList();
        if (notExistIds.Count > 0)
        {
            throw new BusinessException("部分模型不存在") { StatusCode = 404 };
        }

        var existingAuths = await _dbContext.AiModelAuthorizations
            .Where(x => x.TeamId == request.TeamId && aimodelIds.Contains(x.AiModelId))
            .Select(x => x.AiModelId)
            .ToListAsync(cancellationToken);

        var toAdd = aimodelIds.Except(existingAuths).ToList();

        if (toAdd.Count > 0)
        {
            var newAuths = toAdd.Select(modelId => new AiModelAuthorizationEntity
            {
                AiModelId = modelId,
                TeamId = request.TeamId
            }).ToList();

            await _dbContext.AiModelAuthorizations.AddRangeAsync(newAuths, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return EmptyCommandResponse.Default;
    }
}
