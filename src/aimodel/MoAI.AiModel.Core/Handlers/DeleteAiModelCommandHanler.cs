using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AiModel.Commands;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.AiModel.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteAiModelCommand"/>
/// </summary>
public class DeleteAiModelCommandHanler : IRequestHandler<DeleteAiModelCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteAiModelCommandHanler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public DeleteAiModelCommandHanler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteAiModelCommand request, CancellationToken cancellationToken)
    {
        var aiModel = await _databaseContext.AiModels.FirstOrDefaultAsync(x => x.Id == request.AiModelId, cancellationToken);

        if (aiModel == null)
        {
            throw new BusinessException("模型不存在") { StatusCode = 404 };
        }

        _databaseContext.AiModels.Remove(aiModel);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
