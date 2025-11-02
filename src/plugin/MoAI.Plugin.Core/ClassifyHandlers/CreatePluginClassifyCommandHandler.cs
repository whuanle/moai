using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Plugin.Commands;

namespace MoAI.Plugin.ClassifyHandlers;

/// <summary>
/// <inheritdoc cref="CreatePluginClassifyCommand"/>
/// </summary>
public class CreatePluginClassifyCommandHandler : IRequestHandler<CreatePluginClassifyCommand, SimpleInt>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreatePluginClassifyCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public CreatePluginClassifyCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<SimpleInt> Handle(CreatePluginClassifyCommand request, CancellationToken cancellationToken)
    {
        var existClassify = await _databaseContext.Classifies
            .AnyAsync(x => x.Type == "plugin" && x.Name == request.Name, cancellationToken);

        if (existClassify == true)
        {
            throw new BusinessException("分类已存在") { StatusCode = 409 };
        }

        var entity = _databaseContext.Classifies.Add(new Database.Entities.ClassifyEntity
        {
            Type = "plugin",
            Name = request.Name,
            Description = request.Description ?? string.Empty
        });

        await _databaseContext.SaveChangesAsync(cancellationToken);
        return new SimpleInt { Value = entity.Entity.Id };
    }
}
