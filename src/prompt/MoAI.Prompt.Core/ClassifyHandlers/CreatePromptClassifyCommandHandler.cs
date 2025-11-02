using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin.Commands;
using MoAI.Prompt.Commands;

namespace MoAI.Prompt.ClassifyHandlers;

/// <summary>
/// <inheritdoc cref="CreatePromptClassifyCommand"/>
/// </summary>
public class CreatePromptClassifyCommandHandler : IRequestHandler<CreatePromptClassifyCommand, SimpleInt>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreatePromptClassifyCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public CreatePromptClassifyCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<SimpleInt> Handle(CreatePromptClassifyCommand request, CancellationToken cancellationToken)
    {
        var existClassify = await _databaseContext.Classifies
            .AnyAsync(x => x.Type == "prompt" && x.Name == request.Name, cancellationToken);

        if (existClassify == true)
        {
            throw new BusinessException("分类已存在") { StatusCode = 409 };
        }

        var entity = _databaseContext.Classifies.Add(new Database.Entities.ClassifyEntity
        {
            Type = "prompt",
            Name = request.Name,
            Description = request.Description ?? string.Empty
        });

        await _databaseContext.SaveChangesAsync(cancellationToken);
        return new SimpleInt { Value = entity.Entity.Id };
    }
}
