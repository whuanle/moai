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

        foreach (var model in models)
        {
            model.IsDeleted = 1;
            _databaseContext.Update(model);
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
