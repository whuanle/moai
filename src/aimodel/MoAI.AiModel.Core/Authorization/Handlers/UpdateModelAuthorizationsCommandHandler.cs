using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AiModel.Authorization.Commands;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.AiModel.Authorization.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateModelAuthorizationsCommand"/>
/// </summary>
public class UpdateModelAuthorizationsCommandHandler : IRequestHandler<UpdateModelAuthorizationsCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateModelAuthorizationsCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    public UpdateModelAuthorizationsCommandHandler(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateModelAuthorizationsCommand request, CancellationToken cancellationToken)
    {
        var model = await _dbContext.AiModels
            .FirstOrDefaultAsync(x => x.Id == request.ModelId, cancellationToken);

        if (model == null)
        {
            throw new BusinessException("模型不存在") { StatusCode = 404 };
        }

        var existingAuths = await _dbContext.AiModelAuthorizations
            .Where(x => x.AiModelId == request.ModelId)
            .ToListAsync(cancellationToken);

        var existingTeamIds = existingAuths.Select(x => x.TeamId).ToHashSet();
        var newTeamIds = request.TeamIds.ToHashSet();

        var toRemove = existingAuths.Where(x => !newTeamIds.Contains(x.TeamId)).ToList();
        var toAdd = newTeamIds.Except(existingTeamIds).ToList();

        if (toRemove.Count > 0)
        {
            _dbContext.AiModelAuthorizations.RemoveRange(toRemove);
        }

        if (toAdd.Count > 0)
        {
            var validTeams = await _dbContext.Teams
                .Where(x => toAdd.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            var now = DateTimeOffset.UtcNow;
            var newAuths = validTeams.Select(teamId => new AiModelAuthorizationEntity
            {
                AiModelId = request.ModelId,
                TeamId = teamId
            }).ToList();

            await _dbContext.AiModelAuthorizations.AddRangeAsync(newAuths, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
