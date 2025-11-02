using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Wiki.Wikis.Commands;

namespace MoAI.Wiki.Wikis.Handlers;

/// <summary>
/// 创建知识库.
/// </summary>
public class CreateWikiCommandHandler : IRequestHandler<CreateWikiCommand, SimpleInt>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateWikiCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public CreateWikiCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<SimpleInt> Handle(CreateWikiCommand request, CancellationToken cancellationToken)
    {
        // 个人知识库可以随便起名，重复没关系
        if (request.IsSystem)
        {
            var exist = await _databaseContext.Wikis.AnyAsync(x => x.IsSystem&& x.Name == request.Name, cancellationToken);
            if (exist)
            {
                throw new BusinessException("系统知识库名称重复") { StatusCode = 409 };
            }
        }

        var wikiEntity = new WikiEntity
        {
            Name = request.Name,
            Description = request.Description,
            EmbeddingModelId = default,
            EmbeddingDimensions = 1024,
            IsSystem = request.IsSystem,
            IsPublic = false,
            IsLock = false
        };

        await _databaseContext.Wikis.AddAsync(wikiEntity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new SimpleInt
        {
            Value = wikiEntity.Id
        };
    }
}
