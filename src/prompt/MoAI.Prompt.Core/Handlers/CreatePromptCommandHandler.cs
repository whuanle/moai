using MediatR;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Models;
using MoAI.Prompt.Commands;

namespace MoAIPrompt.Core.Handlers;

/// <summary>
/// <inheritdoc cref="CreatePromptCommand"/>
/// </summary>
public class CreatePromptCommandHandler : IRequestHandler<CreatePromptCommand, SimpleInt>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreatePromptCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public CreatePromptCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<SimpleInt> Handle(CreatePromptCommand request, CancellationToken cancellationToken)
    {
        var prompt = new PromptEntity
        {
            Name = request.Name,
            Description = request.Description,
            Content = request.Content,
            PromptClassId = request.PromptClassId,
            IsPublic = false
        };

        await _databaseContext.Prompts.AddAsync(prompt, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return prompt.Id;
    }
}
