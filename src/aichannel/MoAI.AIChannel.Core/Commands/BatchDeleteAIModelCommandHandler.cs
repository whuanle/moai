using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIChannel.Commands;
using MoAI.Database;
using MoAI.Infra.Models;

namespace MoAI.AIChannel.Handlers;

/// <summary>
/// <inheritdoc cref="BatchDeleteAIModelCommand"/>
/// </summary>
public class BatchDeleteAIModelCommandHandler : IRequestHandler<BatchDeleteAIModelCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchDeleteAIModelCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public BatchDeleteAIModelCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(BatchDeleteAIModelCommand request, CancellationToken cancellationToken)
    {
        var models = await _databaseContext.AiModels
            .Where(x => request.ModelIds.Contains(x.Id) && x.IsDeleted == 0)
            .ToListAsync(cancellationToken);

        await EnsureModelsInTeamScopeAsync(models, request.TeamId, cancellationToken);

        foreach (var model in models)
        {
            model.IsDeleted = 1;
            _databaseContext.Update(model);
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }

    private async Task EnsureModelsInTeamScopeAsync(List<MoAI.Database.Entities.AiModelEntity> models, int teamId, CancellationToken cancellationToken)
    {
        if (teamId <= 0 || models.Count == 0)
        {
            return;
        }

        var channelIds = models.Select(x => x.ChannelId).Distinct().ToList();
        var foreignCount = await _databaseContext.AiChannels
            .CountAsync(x => channelIds.Contains(x.Id) && x.TeamId != teamId, cancellationToken);
        if (foreignCount > 0)
        {
            throw new MoAI.Infra.Exceptions.BusinessException("包含不在本团队渠道下的模型.") { StatusCode = 404 };
        }
    }
}
