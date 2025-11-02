using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.Wiki.WebDocuments.Commands;

/// <summary>
/// <inheritdoc cref="AddWebDocumentConfigCommand"/>
/// </summary>
public class AddWebDocumentConfigCommandHandler : IRequestHandler<AddWebDocumentConfigCommand, SimpleInt>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddWebDocumentConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public AddWebDocumentConfigCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<SimpleInt> Handle(AddWebDocumentConfigCommand request, CancellationToken cancellationToken)
    {
        var existWiki = await _databaseContext.Wikis.AnyAsync(x => x.Id == request.WikiId, cancellationToken);
        if (!existWiki)
        {
            throw new BusinessException("知识库不存在") { StatusCode = 404 };
        }

        var entity = new WikiWebConfigEntity
        {
            WikiId = request.WikiId,
            Title = request.Title,
            Address = request.Address.ToString(),
            IsCrawlOther = request.IsCrawlOther,
            IsAutoEmbedding = request.IsAutoEmbedding,
            IsWaitJs = request.IsWaitJs,
            LimitAddress = request.LimitAddress?.ToString() ?? string.Empty,
            LimitMaxCount = request.LimitMaxCount,
            Selector = string.Empty
        };

        _databaseContext.WikiWebConfigs.Add(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return new SimpleInt { Value = entity.Id };
    }
}
