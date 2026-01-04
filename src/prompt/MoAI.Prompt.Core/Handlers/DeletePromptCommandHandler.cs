using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Prompt.Commands;

namespace MoAIPrompt.Core.Handlers;

/// <summary>
/// <inheritdoc cref="DeletePromptCommand"/>
/// </summary>
public class DeletePromptCommandHandler : IRequestHandler<DeletePromptCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeletePromptCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public DeletePromptCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeletePromptCommand request, CancellationToken cancellationToken)
    {
        var promptEntity = await _databaseContext.Prompts.FirstOrDefaultAsync(x => x.Id == request.PromptId);
        if (promptEntity == null)
        {
            throw new BusinessException("未找到提示词") { StatusCode = 404 };
        }

        if (promptEntity.CreateUserId != request.ContextUserId)
        {
            throw new BusinessException("未找到提示词") { StatusCode = 404 };
        }

        _databaseContext.Prompts.Remove(promptEntity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
