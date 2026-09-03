using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Classify.Commands;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.Classify.Handlers;

/// <summary>
/// <inheritdoc cref="CreateClassifyCommand"/>
/// </summary>
public class CreateClassifyCommandHandler : IRequestHandler<CreateClassifyCommand, SimpleInt>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateClassifyCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public CreateClassifyCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<SimpleInt> Handle(CreateClassifyCommand request, CancellationToken cancellationToken)
    {
        var exist = await _databaseContext.Classifies
            .AnyAsync(x => x.Type == request.Type && x.Name == request.Name && x.IsDeleted == 0, cancellationToken);

        if (exist)
        {
            throw new BusinessException("分类名称已存在，请更换后重试.") { StatusCode = 409 };
        }

        var classify = new ClassifyEntity
        {
            Type = request.Type,
            Name = request.Name,
            Description = request.Description ?? string.Empty,
        };

        _databaseContext.Classifies.Add(classify);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return (SimpleInt)classify.Id;
    }
}
