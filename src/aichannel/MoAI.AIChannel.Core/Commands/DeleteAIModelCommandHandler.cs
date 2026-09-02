using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIChannel.Commands;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.AIChannel.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteAIModelCommand"/>
/// </summary>
public class DeleteAIModelCommandHandler : IRequestHandler<DeleteAIModelCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteAIModelCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public DeleteAIModelCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteAIModelCommand request, CancellationToken cancellationToken)
    {
        var model = await _databaseContext.AiModels
            .FirstOrDefaultAsync(x => x.Id == request.ModelId && x.IsDeleted == 0, cancellationToken);

        if (model == null)
        {
            throw new BusinessException("未找到模型，请检查 id 是否正确.") { StatusCode = 404 };
        }

        model.IsDeleted = 1;
        _databaseContext.Update(model);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
