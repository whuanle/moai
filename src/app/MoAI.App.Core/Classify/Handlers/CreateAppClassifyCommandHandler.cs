using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Classify.Commands;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.App.Classify.Handlers;

/// <summary>
/// <inheritdoc cref="CreateAppClassifyCommand"/>
/// </summary>
public class CreateAppClassifyCommandHandler : IRequestHandler<CreateAppClassifyCommand, SimpleInt>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateAppClassifyCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public CreateAppClassifyCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<SimpleInt> Handle(CreateAppClassifyCommand request, CancellationToken cancellationToken)
    {
        var existClassify = await _databaseContext.Classifies
            .AnyAsync(x => x.Type == "app" && x.Name == request.Name, cancellationToken);

        if (existClassify)
        {
            throw new BusinessException("分类已存在") { StatusCode = 409 };
        }

        var entity = _databaseContext.Classifies.Add(new ClassifyEntity
        {
            Type = "app",
            Name = request.Name,
            Description = request.Description
        });

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new SimpleInt { Value = entity.Entity.Id };
    }
}
