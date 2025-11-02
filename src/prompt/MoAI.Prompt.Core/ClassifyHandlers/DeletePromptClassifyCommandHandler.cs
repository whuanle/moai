using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.Prompt.Commands;

namespace MoAI.Prompt.ClassifyHandlers;

/// <summary>
/// <inheritdoc/> cref="DeletePromptClassifyCommandHandler"/>
/// </summary>
public class DeletePromptClassifyCommandHandler : IRequestHandler<DeletePromptClassifyCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeletePromptClassifyCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public DeletePromptClassifyCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeletePromptClassifyCommand request, CancellationToken cancellationToken)
    {
        var classifyEntity = await _databaseContext.Classifies.FirstOrDefaultAsync(x => x.Id == request.ClassifyId && x.Type == "prompt", cancellationToken);
        if (classifyEntity != null)
        {
            _databaseContext.Classifies.Remove(classifyEntity);
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }

        return EmptyCommandResponse.Default;
    }
}
