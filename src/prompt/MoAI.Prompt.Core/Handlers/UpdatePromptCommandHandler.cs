using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Prompt.Commands;

namespace MoAIPrompt.Core.Handlers;

/// <summary>
/// <inheritdoc cref="UpdatePromptCommand"/>
/// </summary>
public class UpdatePromptCommandHandler : IRequestHandler<UpdatePromptCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdatePromptCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public UpdatePromptCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdatePromptCommand request, CancellationToken cancellationToken)
    {
        var prompt = await _databaseContext.Prompts.FirstOrDefaultAsync(x => x.Id == request.PromptId);
        if (prompt == null)
        {
            throw new BusinessException("提示词不存在") { StatusCode = 404 };
        }

        prompt.PromptClassId = request.PromptClassId;
        prompt.IsPublic = request.IsPublic;
        prompt.Content = request.Content;
        prompt.Name = request.Name;
        prompt.Description = request.Description;

        _databaseContext.Prompts.Update(prompt);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}